using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using EnvironmentSetup.App.Models;
using EnvironmentSetup.App.Services;

namespace EnvironmentSetup.App.Steps;

/// <summary>
/// ステップ7: メダリオンアーキテクチャ構築 - Bronze/Silver/Gold Lakehouse作成、ETL Notebook注入・実行
/// </summary>
public class Step07MedallionSetup : ISetupStep
{
    private readonly AzureCliWrapper _az;
    private const string FabricResource = "https://api.fabric.microsoft.com";
    private const string OneLakeDfsBase = "https://onelake.dfs.fabric.microsoft.com";

    public int StepNumber => 7;
    public string Name => "メダリオンアーキテクチャ構築";

    public Step07MedallionSetup(AzureCliWrapper az)
    {
        _az = az;
    }

    public async Task ExecuteAsync(SetupState state, CancellationToken ct = default)
    {
        var azure = state.Azure;
        if (azure != null && !azure.FabricAvailable)
        {
            Console.WriteLine("  ⏭️  Fabric が無効のためスキップします (FabricAvailable=false)");
            return;
        }

        var analysis = state.Analysis
            ?? throw new InvalidOperationException("分析が未完了です。Step 2 を先に実行してください。");

        Console.WriteLine("  Fabric メダリオンアーキテクチャを構築中...\n");

        // 1. ワークスペース特定
        var wsId = await ResolveWorkspaceAsync(state);
        Console.WriteLine($"    ワークスペース: {wsId}\n");

        // 2. Lakehouse 3層の作成 (Bronze / Silver / Gold)
        var bronze = await EnsureLakehouseAsync(wsId, "lh_bronze", ct);
        var silver = await EnsureLakehouseAsync(wsId, "lh_silver", ct);
        var gold = await EnsureLakehouseAsync(wsId, "lh_gold", ct);

        Console.WriteLine($"    Bronze: {bronze.Name} ({bronze.Id})");
        Console.WriteLine($"    Silver: {silver.Name} ({silver.Id})");
        Console.WriteLine($"    Gold:   {gold.Name} ({gold.Id})\n");

        // 3. Seed データを Bronze にアップロード
        await UploadSeedDataAsync(wsId, bronze, analysis, ct);

        // 4. ETL Notebook を作成
        var nbBronzeToSilver = await EnsureNotebookAsync(wsId, "nb_bronze_to_silver",
            BuildBronzeToSilverCode(analysis, bronze, silver), silver.Id, ct);
        var nbSilverToGold = await EnsureNotebookAsync(wsId, "nb_silver_to_gold",
            BuildSilverToGoldCode(analysis, silver, gold), gold.Id, ct);

        Console.WriteLine($"    Notebook (Bronze→Silver): {nbBronzeToSilver}");
        Console.WriteLine($"    Notebook (Silver→Gold):   {nbSilverToGold}\n");

        // 5. Notebook を実行
        Console.WriteLine("    ETL Notebook を実行中...");
        await RunNotebookAsync(wsId, nbBronzeToSilver, ct);
        Console.WriteLine("      ✓ Bronze → Silver 完了");
        await RunNotebookAsync(wsId, nbSilverToGold, ct);
        Console.WriteLine("      ✓ Silver → Gold 完了");

        // 6. state に保存
        state.Medallion = new MedallionResult
        {
            WorkspaceId = wsId,
            BronzeLakehouseId = bronze.Id,
            SilverLakehouseId = silver.Id,
            GoldLakehouseId = gold.Id,
            BronzeToSilverNotebookId = nbBronzeToSilver,
            SilverToGoldNotebookId = nbSilverToGold
        };

        Console.WriteLine($"\n  ✓ メダリオンアーキテクチャ構築完了");
    }

    private async Task<string> ResolveWorkspaceAsync(SetupState state)
    {
        if (!string.IsNullOrEmpty(state.Medallion?.WorkspaceId))
            return state.Medallion.WorkspaceId;

        const string targetWsName = "ws-nexus6-medallion";

        // 既存ワークスペースを検索
        var wsJson = await _az.RunAsync(
            $"rest --method get --url \"{FabricResource}/v1/workspaces\" --resource \"{FabricResource}\"",
            silent: true);
        var doc = JsonDocument.Parse(wsJson);

        var existing = doc.RootElement.GetProperty("value").EnumerateArray()
            .FirstOrDefault(w => w.GetProperty("displayName").GetString() == targetWsName);

        if (existing.ValueKind != JsonValueKind.Undefined)
        {
            var wsId = existing.GetProperty("id").GetString()!;
            // ワークスペースが実際に使用可能か確認
            try
            {
                await _az.RunAsync(
                    $"rest --method get --url \"{FabricResource}/v1/workspaces/{wsId}\" --resource \"{FabricResource}\"",
                    silent: true);
                await EnsureCapacityAssignedAsync(wsId, targetWsName);
                return wsId;
            }
            catch
            {
                // ゴースト状態のワークスペース（一覧に出るが使えない）→ 新規作成
                Console.WriteLine($"    ⚠️ ワークスペース '{targetWsName}' が応答しないため再作成します...");
            }
        }

        // capacity ID を取得
        Console.WriteLine($"    ワークスペース '{targetWsName}' を作成中...");
        var capJson = await _az.RunAsync(
            $"rest --method get --url \"{FabricResource}/v1/capacities\" --resource \"{FabricResource}\"",
            silent: true);
        var capDoc = JsonDocument.Parse(capJson);
        var capacityId = capDoc.RootElement.GetProperty("value").EnumerateArray()
            .Where(c => c.GetProperty("state").GetString() == "Active")
            .Select(c => c.GetProperty("id").GetString())
            .FirstOrDefault()
            ?? throw new InvalidOperationException("アクティブな Fabric Capacity が見つかりません。");

        // ワークスペース作成
        var payload = JsonSerializer.Serialize(new { displayName = targetWsName, capacityId });
        var payloadPath = Path.GetFullPath("./output/ws_create.json");
        Directory.CreateDirectory(Path.GetDirectoryName(payloadPath)!);
        await File.WriteAllTextAsync(payloadPath, payload);

        var result = await _az.RunAsync(
            $"rest --method POST --url \"{FabricResource}/v1/workspaces\" --resource \"{FabricResource}\" " +
            $"--headers \"Content-Type=application/json\" --body @{payloadPath}",
            silent: true);

        var wsDoc = JsonDocument.Parse(result);
        return wsDoc.RootElement.GetProperty("id").GetString()!;
    }

    private async Task EnsureCapacityAssignedAsync(string wsId, string wsName)
    {
        // キャパシティを取得 (Active または Inactive)
        var capJson = await _az.RunAsync(
            $"rest --method get --url \"{FabricResource}/v1/capacities\" --resource \"{FabricResource}\"",
            silent: true);
        var capDoc = JsonDocument.Parse(capJson);

        // まず Active を探す
        var activeId = capDoc.RootElement.GetProperty("value").EnumerateArray()
            .Where(c => c.GetProperty("state").GetString() == "Active")
            .Select(c => c.GetProperty("id").GetString())
            .FirstOrDefault();

        if (string.IsNullOrEmpty(activeId))
        {
            // Inactive なキャパシティを Resume する
            var inactiveEntry = capDoc.RootElement.GetProperty("value").EnumerateArray()
                .FirstOrDefault();

            if (inactiveEntry.ValueKind == JsonValueKind.Undefined)
            {
                throw new InvalidOperationException(
                    "Fabric Capacity が存在しません。Bicep デプロイ (deployFabric=true) を確認してください。");
            }

            var inactiveId = inactiveEntry.GetProperty("id").GetString()!;
            var capacityDisplayName = inactiveEntry.GetProperty("displayName").GetString()!;
            Console.WriteLine($"    🔄 Fabric Capacity '{capacityDisplayName}' が Inactive → Resume 中...");

            // ARM REST API で Resume
            try
            {
                var subId = (await _az.RunAsync("account show --query id -o tsv", silent: true)).Trim();
                var rg = (await _az.RunAsync(
                    "group list --query \"[?contains(name,'nexus6')].name | [0]\" -o tsv", silent: true)).Trim();
                await _az.RunAsync(
                    $"rest --method POST " +
                    $"--url \"https://management.azure.com/subscriptions/{subId}/resourceGroups/{rg}/providers/Microsoft.Fabric/capacities/{capacityDisplayName}/resume?api-version=2023-11-01\" " +
                    $"--resource \"https://management.azure.com\"",
                    silent: true);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Fabric Capacity の Resume に失敗しました。Azure Portal から手動で Resume してください: {ex.Message}");
            }

            // Resume 完了待ち (最大3分)
            Console.Write("    Resume 待機中");
            for (int i = 0; i < 12; i++)
            {
                await Task.Delay(15000);
                var checkJson = await _az.RunAsync(
                    $"rest --method get --url \"{FabricResource}/v1/capacities\" --resource \"{FabricResource}\"",
                    silent: true);
                var checkDoc = JsonDocument.Parse(checkJson);
                var capState = checkDoc.RootElement.GetProperty("value").EnumerateArray()
                    .Where(c => c.GetProperty("id").GetString() == inactiveId)
                    .Select(c => c.GetProperty("state").GetString())
                    .FirstOrDefault();
                if (capState == "Active")
                {
                    Console.WriteLine(" ✓ Active");
                    activeId = inactiveId;
                    break;
                }
                Console.Write(".");
            }

            if (string.IsNullOrEmpty(activeId))
            {
                throw new InvalidOperationException(
                    "Fabric Capacity が3分以内に Active になりませんでした。Azure Portal を確認してください。");
            }
        }

        var capacityId = activeId;

        // キャパシティを割り当て
        Console.WriteLine($"    ワークスペース '{wsName}' にキャパシティを割り当て中...");
        var payload = JsonSerializer.Serialize(new { capacityId });
        var payloadPath = Path.GetFullPath("./output/ws_capacity.json");
        Directory.CreateDirectory(Path.GetDirectoryName(payloadPath)!);
        await File.WriteAllTextAsync(payloadPath, payload);

        try
        {
            await _az.RunAsync(
                $"rest --method POST --url \"{FabricResource}/v1/workspaces/{wsId}/assignToCapacity\" " +
                $"--resource \"{FabricResource}\" --headers \"Content-Type=application/json\" --body @{payloadPath}",
                silent: true);
            Console.WriteLine($"    ✓ キャパシティ割り当て完了");
            // 割り当て反映まで少し待機
            await Task.Delay(5000);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    ⚠️ キャパシティ割り当て: {ex.Message[..Math.Min(200, ex.Message.Length)]}");
        }
    }

    private async Task<LakehouseInfo> EnsureLakehouseAsync(string wsId, string name, CancellationToken ct)
    {
        // 既存チェック
        var itemsJson = await _az.RunAsync(
            $"rest --method get --url \"{FabricResource}/v1/workspaces/{wsId}/items?type=Lakehouse\" --resource \"{FabricResource}\"",
            silent: true);
        var doc = JsonDocument.Parse(itemsJson);
        var existing = doc.RootElement.GetProperty("value").EnumerateArray()
            .FirstOrDefault(i => i.GetProperty("displayName").GetString() == name);

        if (existing.ValueKind != JsonValueKind.Undefined)
        {
            return new LakehouseInfo
            {
                Id = existing.GetProperty("id").GetString()!,
                Name = name
            };
        }

        // 作成
        Console.WriteLine($"    Lakehouse '{name}' を作成中...");
        var payload = JsonSerializer.Serialize(new { displayName = name, type = "Lakehouse" });
        var payloadPath = Path.GetFullPath($"./output/lh_{name}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(payloadPath)!);
        await File.WriteAllTextAsync(payloadPath, payload, ct);

        var opId = await ExecuteFabricLroAsync(
            $"rest --method POST --url \"{FabricResource}/v1/workspaces/{wsId}/items\" " +
            $"--resource \"{FabricResource}\" --headers \"Content-Type=application/json\" " +
            $"--body @{payloadPath} --verbose");

        if (opId != null)
            await PollLroAsync(opId, ct);

        // 作成されたIDを取得
        itemsJson = await _az.RunAsync(
            $"rest --method get --url \"{FabricResource}/v1/workspaces/{wsId}/items?type=Lakehouse\" --resource \"{FabricResource}\"",
            silent: true);
        doc = JsonDocument.Parse(itemsJson);
        var created = doc.RootElement.GetProperty("value").EnumerateArray()
            .First(i => i.GetProperty("displayName").GetString() == name);

        return new LakehouseInfo
        {
            Id = created.GetProperty("id").GetString()!,
            Name = name
        };
    }

    private async Task UploadSeedDataAsync(string wsId, LakehouseInfo bronze, AnalysisResult analysis, CancellationToken ct)
    {
        Console.WriteLine("    Seed データを Bronze にアップロード中...");

        // テーブル定義からCSVシードデータを生成して OneLake にアップロード
        var seedDir = Path.GetFullPath("./output/seed");
        Directory.CreateDirectory(seedDir);

        foreach (var table in analysis.Tables)
        {
            var csvPath = Path.Combine(seedDir, $"{table.TableName}.csv");
            if (!File.Exists(csvPath))
            {
                // ヘッダー行のみのCSVを生成 (実データは Step06 で SQL に投入済み)
                var header = string.Join(",", table.Columns.Select(c => c.Name));
                await File.WriteAllTextAsync(csvPath, header + "\n", ct);
            }

            // OneLake DFS API でアップロード
            var oneLakePath = $"{wsId}/{bronze.Id}/Files/seed/{table.TableName}.csv";
            try
            {
                var token = await GetStorageTokenAsync();
                await UploadToOneLakeAsync(oneLakePath, csvPath, token);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"      ⚠️ {table.TableName}.csv アップロードスキップ: {ex.Message}");
            }
        }

        Console.WriteLine("      ✓ Seed アップロード完了");
    }

    private async Task<string> GetStorageTokenAsync()
    {
        return await _az.RunAsync(
            "account get-access-token --resource https://storage.azure.com --query accessToken -o tsv",
            silent: true);
    }

    private async Task UploadToOneLakeAsync(string oneLakePath, string localPath, string token)
    {
        var url = $"{OneLakeDfsBase}/{oneLakePath}?resource=file";

        // Create file
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "curl",
            Arguments = $"-s -X PUT \"{url}\" -H \"Authorization: Bearer {token}\" --fail",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var createProc = System.Diagnostics.Process.Start(psi)!;
        await createProc.WaitForExitAsync();

        // Append data
        var content = await File.ReadAllBytesAsync(localPath);
        var appendUrl = $"{OneLakeDfsBase}/{oneLakePath}?action=append&position=0";
        psi.Arguments = $"-s -X PATCH \"{appendUrl}\" -H \"Authorization: Bearer {token}\" " +
                        $"-H \"Content-Type: application/octet-stream\" --data-binary @{localPath} --fail";
        using var appendProc = System.Diagnostics.Process.Start(psi)!;
        await appendProc.WaitForExitAsync();

        // Flush
        var flushUrl = $"{OneLakeDfsBase}/{oneLakePath}?action=flush&position={content.Length}";
        psi.Arguments = $"-s -X PATCH \"{flushUrl}\" -H \"Authorization: Bearer {token}\" --fail";
        using var flushProc = System.Diagnostics.Process.Start(psi)!;
        await flushProc.WaitForExitAsync();
    }

    private async Task<string> EnsureNotebookAsync(
        string wsId, string name, string pySparkCode, string defaultLakehouseId, CancellationToken ct)
    {
        // 既存チェック
        var itemsJson = await _az.RunAsync(
            $"rest --method get --url \"{FabricResource}/v1/workspaces/{wsId}/items?type=Notebook\" --resource \"{FabricResource}\"",
            silent: true);
        var doc = JsonDocument.Parse(itemsJson);
        var existing = doc.RootElement.GetProperty("value").EnumerateArray()
            .FirstOrDefault(i => i.GetProperty("displayName").GetString() == name);

        if (existing.ValueKind != JsonValueKind.Undefined)
            return existing.GetProperty("id").GetString()!;

        // Notebook 定義を構築
        Console.WriteLine($"    Notebook '{name}' を作成中...");

        var notebookContent = BuildNotebookJson(pySparkCode, defaultLakehouseId, wsId);
        var contentB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(notebookContent));

        var platformJson = JsonSerializer.Serialize(new
        {
            metadata = new { type = "Notebook", displayName = name },
            config = new { version = "2.0", logicalId = "00000000-0000-0000-0000-000000000000" }
        });
        var platformB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(platformJson));

        var envelope = new JsonObject
        {
            ["displayName"] = name,
            ["type"] = "Notebook",
            ["definition"] = new JsonObject
            {
                ["parts"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["path"] = "notebook-content.py",
                        ["payload"] = contentB64,
                        ["payloadType"] = "InlineBase64"
                    },
                    new JsonObject
                    {
                        ["path"] = ".platform",
                        ["payload"] = platformB64,
                        ["payloadType"] = "InlineBase64"
                    }
                }
            }
        };

        var envPath = Path.GetFullPath($"./output/{name}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(envPath)!);
        await File.WriteAllTextAsync(envPath, envelope.ToJsonString(), ct);

        var opId = await ExecuteFabricLroAsync(
            $"rest --method POST --url \"{FabricResource}/v1/workspaces/{wsId}/items\" " +
            $"--resource \"{FabricResource}\" --headers \"Content-Type=application/json\" " +
            $"--body @{envPath} --verbose");

        if (opId != null)
            await PollLroAsync(opId, ct);

        // 作成されたIDを取得
        itemsJson = await _az.RunAsync(
            $"rest --method get --url \"{FabricResource}/v1/workspaces/{wsId}/items?type=Notebook\" --resource \"{FabricResource}\"",
            silent: true);
        doc = JsonDocument.Parse(itemsJson);
        var created = doc.RootElement.GetProperty("value").EnumerateArray()
            .First(i => i.GetProperty("displayName").GetString() == name);

        return created.GetProperty("id").GetString()!;
    }

    private async Task RunNotebookAsync(string wsId, string notebookId, CancellationToken ct)
    {
        var opId = await ExecuteFabricLroAsync(
            $"rest --method POST --url \"{FabricResource}/v1/workspaces/{wsId}/items/{notebookId}/jobs/instances?jobType=RunNotebook\" " +
            $"--resource \"{FabricResource}\" --headers \"Content-Type=application/json\" " +
            "--body \"{}\" --verbose");

        // Notebook 実行は時間がかかるためタイムアウトを延長
        if (opId != null)
            await PollLroAsync(opId, ct, maxAttempts: 60, intervalSeconds: 10);
    }

    private static string BuildNotebookJson(string pySparkCode, string lakehouseId, string wsId)
    {
        // Fabric Notebook の .py 形式 (# Fabric notebook source)
        var sb = new StringBuilder();
        sb.AppendLine("# Fabric notebook source");
        sb.AppendLine();
        sb.AppendLine("# METADATA ********************");
        sb.AppendLine();
        sb.AppendLine("# META {");
        sb.AppendLine($"# META   \"kernel_info\": {{\"name\": \"synapse_pyspark\"}},");
        sb.AppendLine($"# META   \"dependencies\": {{\"lakehouse\": {{\"default_lakehouse\": \"{lakehouseId}\", \"default_lakehouse_name\": \"\", \"default_lakehouse_workspace_id\": \"{wsId}\"}}}}");
        sb.AppendLine("# META }");
        sb.AppendLine();
        sb.AppendLine("# CELL ********************");
        sb.AppendLine();
        sb.AppendLine(pySparkCode);

        return sb.ToString();
    }

    private static string BuildBronzeToSilverCode(AnalysisResult analysis, LakehouseInfo bronze, LakehouseInfo silver)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Bronze → Silver ETL: データクレンジングと型変換");
        sb.AppendLine("from pyspark.sql import SparkSession");
        sb.AppendLine("from pyspark.sql.functions import col, trim, current_timestamp");
        sb.AppendLine();
        sb.AppendLine("spark = SparkSession.builder.getOrCreate()");
        sb.AppendLine();

        foreach (var table in analysis.Tables)
        {
            sb.AppendLine($"# --- {table.TableName} ---");
            sb.AppendLine($"try:");
            sb.AppendLine($"    df_{table.TableName} = spark.read.format(\"csv\").option(\"header\", \"true\").load(\"Files/seed/{table.TableName}.csv\")");
            sb.AppendLine($"    df_{table.TableName} = df_{table.TableName}.withColumn(\"_ingested_at\", current_timestamp())");

            // 型変換
            foreach (var col in table.Columns)
            {
                var sparkType = MapToSparkType(col.Type);
                if (sparkType != "string")
                    sb.AppendLine($"    df_{table.TableName} = df_{table.TableName}.withColumn(\"{col.Name}\", col(\"{col.Name}\").cast(\"{sparkType}\"))");
            }

            sb.AppendLine($"    df_{table.TableName}.write.mode(\"overwrite\").format(\"delta\").saveAsTable(\"{table.TableName}\")");
            sb.AppendLine($"    print(f\"{table.TableName}: {{df_{table.TableName}.count()}} rows written to Silver\")");
            sb.AppendLine($"except Exception as e:");
            sb.AppendLine($"    print(f\"{table.TableName}: skipped - {{e}}\")");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string BuildSilverToGoldCode(AnalysisResult analysis, LakehouseInfo silver, LakehouseInfo gold)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Silver → Gold ETL: KPI集計とビジネスビュー作成");
        sb.AppendLine("from pyspark.sql import SparkSession");
        sb.AppendLine("from pyspark.sql.functions import col, sum as _sum, count, avg, max as _max, lit");
        sb.AppendLine();
        sb.AppendLine("spark = SparkSession.builder.getOrCreate()");
        sb.AppendLine();

        // テーブル読み込み
        foreach (var table in analysis.Tables)
        {
            sb.AppendLine($"try:");
            sb.AppendLine($"    {table.TableName} = spark.read.format(\"delta\").table(\"{table.TableName}\")");
            sb.AppendLine($"except: {table.TableName} = None");
        }

        sb.AppendLine();
        sb.AppendLine("# Gold テーブルとしてそのまま公開 (集計ロジックはドメインに依存)");
        foreach (var table in analysis.Tables)
        {
            sb.AppendLine($"if {table.TableName} is not None:");
            sb.AppendLine($"    {table.TableName}.write.mode(\"overwrite\").format(\"delta\").saveAsTable(\"gold_{table.TableName}\")");
            sb.AppendLine($"    print(f\"gold_{table.TableName}: {{{table.TableName}.count()}} rows\")");
        }

        return sb.ToString();
    }

    private static string MapToSparkType(string sqlType)
    {
        var normalized = sqlType.ToLowerInvariant().Trim();
        return normalized switch
        {
            "int" or "smallint" or "tinyint" => "int",
            "bigint" => "long",
            "float" or "real" => "float",
            "double" or "decimal" or "numeric" or "money" => "double",
            "bit" or "boolean" => "boolean",
            "datetime" or "datetime2" or "date" or "timestamp" or "datetimeoffset" => "timestamp",
            _ => "string"
        };
    }

    private async Task<string?> ExecuteFabricLroAsync(string azArgs)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "az",
            Arguments = azArgs,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = System.Diagnostics.Process.Start(psi)
            ?? throw new InvalidOperationException("az CLI の起動に失敗しました。");

        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            // stderr にINFOログのみでエラー本文がstdoutに含まれる場合がある
            var errorDetail = !string.IsNullOrWhiteSpace(stdout) && stdout.Contains("errorCode")
                ? stdout[..Math.Min(500, stdout.Length)]
                : stderr[..Math.Min(800, stderr.Length)];
            throw new InvalidOperationException(
                $"Fabric API エラー (exit {process.ExitCode}): {errorDetail}");
        }

        // x-ms-operation-id を抽出 (202 LRO の場合)
        var opIdMatch = System.Text.RegularExpressions.Regex.Match(
            stderr, @"x-ms-operation-id['""]?\s*[:=]\s*['""]?([a-f0-9\-]+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (opIdMatch.Success)
            return opIdMatch.Groups[1].Value;

        // Location ヘッダーから operation-id を抽出
        var locMatch = System.Text.RegularExpressions.Regex.Match(
            stderr, @"operations/([a-f0-9\-]+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (locMatch.Success)
            return locMatch.Groups[1].Value;

        // 同期完了 (201) — LRO不要
        return null;
    }

    private async Task PollLroAsync(string operationId, CancellationToken ct, int maxAttempts = 30, int intervalSeconds = 5)
    {
        for (var i = 0; i < maxAttempts; i++)
        {
            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), ct);

            var result = await _az.RunAsync(
                $"rest --method get --url \"{FabricResource}/v1/operations/{operationId}\" --resource \"{FabricResource}\"",
                silent: true);

            var doc = JsonDocument.Parse(result);
            var status = doc.RootElement.GetProperty("status").GetString();

            switch (status)
            {
                case "Succeeded":
                    return;
                case "Failed":
                case "Cancelled":
                    var error = doc.RootElement.TryGetProperty("error", out var errProp)
                        ? errProp.ToString()
                        : "不明なエラー";
                    throw new InvalidOperationException($"Fabric LRO 失敗 ({status}): {error}");
            }
        }

        throw new TimeoutException($"Fabric LRO がタイムアウトしました ({maxAttempts * intervalSeconds}秒)。");
    }

    private class LakehouseInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}
