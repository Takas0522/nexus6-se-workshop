using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Linq;
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

        // ワークスペースIDを早期保存（途中失敗時の cleanup 用）
        state.Medallion ??= new MedallionResult();
        state.Medallion.WorkspaceId = wsId;

        // 2. Lakehouse 3層の作成 (Bronze / Silver / Gold)
        var bronze = await EnsureLakehouseAsync(wsId, "lh_bronze", ct);
        var silver = await EnsureLakehouseAsync(wsId, "lh_silver", ct);
        var gold = await EnsureLakehouseAsync(wsId, "lh_gold", ct);

        Console.WriteLine($"    Bronze: {bronze.Name} ({bronze.Id})");
        Console.WriteLine($"    Silver: {silver.Name} ({silver.Id})");
        Console.WriteLine($"    Gold:   {gold.Name} ({gold.Id})\n");

        // 3. Seed データを Bronze にアップロード
        await UploadSeedDataAsync(wsId, bronze, analysis, ct);

        // 4. 統合 ETL Notebook を作成 (Bronze→Silver→Gold を1つの Notebook で実行)
        // Note: Fabric では Notebook の attach 先 Lakehouse にしか saveAsTable できないため全テーブルを Bronze に集約
        var fullEtlCode = BuildFullMedallionEtlCode(analysis);
        var nbMedallionEtl = await EnsureNotebookAsync(wsId, "nb_medallion_pipeline",
            fullEtlCode, bronze.Id, ct);

        Console.WriteLine($"    Notebook (Medallion ETL): {nbMedallionEtl}\n");

        // 5. Notebook を実行
        Console.WriteLine("    ETL Notebook を実行中...");
        await RunNotebookAsync(wsId, nbMedallionEtl, ct);
        Console.WriteLine("      ✓ Medallion ETL 完了");

        // 6. state に保存
        state.Medallion = new MedallionResult
        {
            WorkspaceId = wsId,
            BronzeLakehouseId = bronze.Id,
            SilverLakehouseId = silver.Id,
            GoldLakehouseId = gold.Id,
            BronzeToSilverNotebookId = nbMedallionEtl,
            SilverToGoldNotebookId = nbMedallionEtl
        };

        // 7. Bronze Lakehouse SQL Endpoint を取得 (全テーブルが Bronze に集約されるため)
        try
        {
            var sqlEndpoint = await GetLakehouseSqlEndpointAsync(wsId, bronze.Id);
            if (!string.IsNullOrEmpty(sqlEndpoint))
            {
                state.Deployment ??= new DeploymentResult();
                state.Deployment.FabricSqlEndpoint = sqlEndpoint;
                state.Deployment.FabricDatabase = "lh_bronze";
                Console.WriteLine($"    SQL Endpoint: {sqlEndpoint}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    ⚠️ SQL Endpoint 取得スキップ: {ex.Message}");
        }

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

            // ARM REST API で Resume — Capacity リソースの実際の RG を特定
            try
            {
                var subId = (await _az.RunAsync("account show --query id -o tsv", silent: true)).Trim();
                var rg = (await _az.RunAsync(
                    $"resource list --resource-type \"Microsoft.Fabric/capacities\" " +
                    $"--query \"[?name=='{capacityDisplayName}'].resourceGroup | [0]\" -o tsv",
                    silent: true)).Trim();
                if (string.IsNullOrEmpty(rg))
                    throw new InvalidOperationException($"Fabric Capacity '{capacityDisplayName}' のリソースグループが見つかりません。");
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

        try
        {
            var opId = await ExecuteFabricLroAsync(
                $"rest --method POST --url \"{FabricResource}/v1/workspaces/{wsId}/items\" " +
                $"--resource \"{FabricResource}\" --headers \"Content-Type=application/json\" " +
                $"--body @{payloadPath}");

            if (opId != null)
                await PollLroAsync(opId, ct);
        }
        catch (InvalidOperationException ex)
        {
            // POST 失敗 → アイテムが既に存在するか再確認 (type付き + type無し両方)
            Console.WriteLine($"    ⚠️ POST 失敗: {ex.Message[..Math.Min(200, ex.Message.Length)]}");
            Console.WriteLine($"    🔄 既存アイテムを再確認中...");
            await Task.Delay(5000, ct);

            // type=Lakehouse でまず検索
            var retryJson = await _az.RunAsync(
                $"rest --method get --url \"{FabricResource}/v1/workspaces/{wsId}/items?type=Lakehouse\" --resource \"{FabricResource}\"",
                silent: true);
            var retryDoc = JsonDocument.Parse(retryJson);
            var found = retryDoc.RootElement.GetProperty("value").EnumerateArray()
                .FirstOrDefault(i => i.GetProperty("displayName").GetString() == name);

            // type フィルタなしでも試行 (Fabric list API の結果整合性対策)
            if (found.ValueKind == JsonValueKind.Undefined)
            {
                var allItemsJson = await _az.RunAsync(
                    $"rest --method get --url \"{FabricResource}/v1/workspaces/{wsId}/items\" --resource \"{FabricResource}\"",
                    silent: true);
                var allDoc = JsonDocument.Parse(allItemsJson);
                found = allDoc.RootElement.GetProperty("value").EnumerateArray()
                    .FirstOrDefault(i => i.GetProperty("displayName").GetString() == name
                        && i.GetProperty("type").GetString() == "Lakehouse");
            }

            if (found.ValueKind != JsonValueKind.Undefined)
            {
                Console.WriteLine($"    ✓ '{name}' は既に存在します (ID: {found.GetProperty("id").GetString()})。続行します。");
                return new LakehouseInfo
                {
                    Id = found.GetProperty("id").GetString()!,
                    Name = name
                };
            }
            // 本当に存在しない場合は元の例外を再スロー
            throw;
        }

        // 作成されたIDを取得 (結果整合性のためリトライ)
        for (var attempt = 0; attempt < 10; attempt++)
        {
            if (attempt > 0)
                await Task.Delay(3000, ct);

            itemsJson = await _az.RunAsync(
                $"rest --method get --url \"{FabricResource}/v1/workspaces/{wsId}/items?type=Lakehouse\" --resource \"{FabricResource}\"",
                silent: true);
            doc = JsonDocument.Parse(itemsJson);
            var created = doc.RootElement.GetProperty("value").EnumerateArray()
                .FirstOrDefault(i => i.GetProperty("displayName").GetString() == name);

            if (created.ValueKind != JsonValueKind.Undefined)
                return new LakehouseInfo
                {
                    Id = created.GetProperty("id").GetString()!,
                    Name = name
                };

            Console.WriteLine($"    ⏳ Lakehouse '{name}' の作成完了を待機中... ({attempt + 1}/10)");
        }

        throw new InvalidOperationException($"Lakehouse '{name}' が作成後にリストに見つかりません。Step 7 を再実行してください。");
    }

    private async Task UploadSeedDataAsync(string wsId, LakehouseInfo bronze, AnalysisResult analysis, CancellationToken ct)
    {
        Console.WriteLine("    Seed データを Bronze にアップロード中...");

        var seedDir = Path.GetFullPath("./output/seed");
        Directory.CreateDirectory(seedDir);

        // output/seed/ 配下の全 CSV をアップロード
        var csvFiles = Directory.GetFiles(seedDir, "*.csv");
        if (csvFiles.Length == 0)
        {
            Console.WriteLine("      ⚠️ Seed CSV が見つかりません。Step 6 を先に実行してください。");
            return;
        }

        var token = await GetStorageTokenAsync();
        var uploaded = 0;
        foreach (var csvPath in csvFiles)
        {
            var fileName = Path.GetFileName(csvPath);
            var fileSize = new FileInfo(csvPath).Length;
            if (fileSize <= 10) // ヘッダーのみファイルはスキップ
                continue;

            var oneLakePath = $"{wsId}/{bronze.Id}/Files/seed/{fileName}";
            try
            {
                await UploadToOneLakeAsync(oneLakePath, csvPath, token);
                uploaded++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"      ⚠️ {fileName} アップロードスキップ: {ex.Message}");
            }
        }

        Console.WriteLine($"      ✓ Seed アップロード完了 ({uploaded}/{csvFiles.Length} ファイル)");
    }

    private async Task<string> GetStorageTokenAsync()
    {
        return await _az.RunAsync(
            "account get-access-token --resource https://storage.azure.com --query accessToken -o tsv",
            silent: true);
    }

    private async Task<string?> GetLakehouseSqlEndpointAsync(string wsId, string lakehouseId)
    {
        // Fabric Lakehouse の SQL analytics endpoint を取得
        var json = await _az.RunAsync(
            $"rest --method get --url \"{FabricResource}/v1/workspaces/{wsId}/lakehouses/{lakehouseId}\" --resource \"{FabricResource}\"",
            silent: true);
        var doc = JsonDocument.Parse(json);

        if (doc.RootElement.TryGetProperty("properties", out var props) &&
            props.TryGetProperty("sqlEndpointProperties", out var sqlProps) &&
            sqlProps.TryGetProperty("connectionString", out var connStr))
        {
            return connStr.GetString();
        }

        // フォールバック: SQL Endpoint アイテムから取得を試みる
        return null;
    }

    private async Task UploadToOneLakeAsync(string oneLakePath, string localPath, string token)
    {
        var url = $"{OneLakeDfsBase}/{oneLakePath}?resource=file";
        var fileContent = await File.ReadAllBytesAsync(localPath);

        // Create file (Content-Length: 0 required by OneLake DFS API)
        await RunCurlAsync($"-s -X PUT \"{url}\" -H \"Authorization: Bearer {token}\" -H \"Content-Length: 0\" --fail");

        // Append data
        var appendUrl = $"{OneLakeDfsBase}/{oneLakePath}?action=append&position=0";
        await RunCurlAsync(
            $"-s -X PATCH \"{appendUrl}\" -H \"Authorization: Bearer {token}\" " +
            $"-H \"Content-Type: application/octet-stream\" -H \"Content-Length: {fileContent.Length}\" " +
            $"--data-binary @{localPath} --fail");

        // Flush
        var flushUrl = $"{OneLakeDfsBase}/{oneLakePath}?action=flush&position={fileContent.Length}";
        await RunCurlAsync($"-s -X PATCH \"{flushUrl}\" -H \"Authorization: Bearer {token}\" -H \"Content-Length: 0\" --fail");
    }

    private static async Task RunCurlAsync(string arguments)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "curl",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var proc = System.Diagnostics.Process.Start(psi)!;
        await proc.WaitForExitAsync();
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

        // Notebook 定義を構築 (.py 形式 — Fabric API は .ipynb path を受け付けない)
        Console.WriteLine($"    Notebook '{name}' を作成中...");

        var contentB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(pySparkCode));

        var platformJson = JsonSerializer.Serialize(new
        {
            metadata = new { type = "Notebook", displayName = name },
            config = new { version = "2.0", logicalId = "00000000-0000-0000-0000-000000000000" },
            dependencies = new
            {
                lakehouse = new
                {
                    default_lakehouse = defaultLakehouseId,
                    default_lakehouse_name = "",
                    default_lakehouse_workspace_id = wsId,
                    known_lakehouses = new[] { new { id = defaultLakehouseId } }
                }
            }
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

        try
        {
            var opId = await ExecuteFabricLroAsync(
                $"rest --method POST --url \"{FabricResource}/v1/workspaces/{wsId}/items\" " +
                $"--resource \"{FabricResource}\" --headers \"Content-Type=application/json\" " +
                $"--body @{envPath}");

            if (opId != null)
                await PollLroAsync(opId, ct);
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("AlreadyInUse", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("Conflict", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("409", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"    ℹ️ Notebook '{name}' は既に存在します。続行します。");
        }

        // 作成されたIDを取得 (結果整合性のためリトライ)
        for (var attempt = 0; attempt < 20; attempt++)
        {
            if (attempt > 0)
                await Task.Delay(5000, ct);

            itemsJson = await _az.RunAsync(
                $"rest --method get --url \"{FabricResource}/v1/workspaces/{wsId}/items?type=Notebook\" --resource \"{FabricResource}\"",
                silent: true);
            doc = JsonDocument.Parse(itemsJson);
            var created = doc.RootElement.GetProperty("value").EnumerateArray()
                .FirstOrDefault(i => i.GetProperty("displayName").GetString() == name);

            if (created.ValueKind != JsonValueKind.Undefined)
                return created.GetProperty("id").GetString()!;

            Console.WriteLine($"    ⏳ Notebook '{name}' の作成完了を待機中... ({attempt + 1}/20)");
        }

        throw new InvalidOperationException($"Notebook '{name}' が作成後にリストに見つかりません。Fabric API の結果整合性の問題の可能性があります。Step 7 を再実行してください。");
    }

    private async Task RunNotebookAsync(string wsId, string notebookId, CancellationToken ct)
    {
        var opId = await ExecuteFabricLroAsync(
            $"rest --method POST --url \"{FabricResource}/v1/workspaces/{wsId}/items/{notebookId}/jobs/instances?jobType=RunNotebook\" " +
            $"--resource \"{FabricResource}\" --headers \"Content-Type=application/json\" " +
            "--body \"{}\"");

        // Notebook 実行は時間がかかるためタイムアウトを延長
        if (opId != null)
            await PollLroAsync(opId, ct, maxAttempts: 60, intervalSeconds: 10);
    }

    private static string BuildNotebookJson(string pySparkCode, string lakehouseId, string wsId)
    {
        // Fabric Notebook: .ipynb 形式 (metadata.dependencies.lakehouse が必須)
        var lines = pySparkCode.Split('\n');
        var sourceLines = new JsonArray();
        for (var i = 0; i < lines.Length; i++)
        {
            sourceLines.Add(i < lines.Length - 1 ? lines[i] + "\n" : lines[i]);
        }

        var notebook = new JsonObject
        {
            ["nbformat"] = 4,
            ["nbformat_minor"] = 5,
            ["metadata"] = new JsonObject
            {
                ["language_info"] = new JsonObject { ["name"] = "python" },
                ["kernel_info"] = new JsonObject { ["name"] = "synapse_pyspark" },
                ["dependencies"] = new JsonObject
                {
                    ["lakehouse"] = new JsonObject
                    {
                        ["default_lakehouse"] = lakehouseId,
                        ["default_lakehouse_name"] = "",
                        ["default_lakehouse_workspace_id"] = wsId,
                        ["known_lakehouses"] = new JsonArray { new JsonObject { ["id"] = lakehouseId } }
                    }
                }
            },
            ["cells"] = new JsonArray
            {
                new JsonObject
                {
                    ["cell_type"] = "code",
                    ["source"] = sourceLines,
                    ["metadata"] = new JsonObject(),
                    ["outputs"] = new JsonArray(),
                    ["execution_count"] = null
                }
            }
        };

        return notebook.ToJsonString();
    }

    private static string BuildFullMedallionEtlCode(AnalysisResult analysis)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Fabric notebook source");
        sb.AppendLine("# Full Medallion ETL: Bronze CSV → Delta Tables + Gold Analytics");
        sb.AppendLine("from pyspark.sql import SparkSession");
        sb.AppendLine("from pyspark.sql.functions import col, current_timestamp, lit, lower, avg, when");
        sb.AppendLine("import os, glob as _glob");
        sb.AppendLine();
        sb.AppendLine("spark = SparkSession.builder.getOrCreate()");
        sb.AppendLine();

        // Phase 1: Bronze → Silver
        sb.AppendLine("# " + new string('=', 60));
        sb.AppendLine("# Phase 1: Bronze → Silver (CSV → Delta + 統合テーブル)");
        sb.AppendLine("# " + new string('=', 60));
        sb.AppendLine("print('Phase 1: Bronze → Silver')");
        sb.AppendLine();

        // 汎用テーブル (analysis.Tables)
        foreach (var table in analysis.Tables)
        {
            sb.AppendLine($"try:");
            sb.AppendLine($"    df = spark.read.format('csv').option('header', 'true').load('Files/seed/{table.TableName}.csv')");
            sb.AppendLine($"    df = df.withColumn('_ingested_at', current_timestamp())");
            foreach (var col in table.Columns)
            {
                var sparkType = MapToSparkType(col.Type);
                if (sparkType != "string")
                    sb.AppendLine($"    df = df.withColumn('{col.Name}', col('{col.Name}').cast('{sparkType}'))");
            }
            sb.AppendLine($"    df.write.mode('overwrite').format('delta').saveAsTable('{table.TableName}')");
            sb.AppendLine($"    print(f'  ✓ {table.TableName}: {{df.count()}} rows')");
            sb.AppendLine($"except Exception as e:");
            sb.AppendLine($"    print(f'  ✗ {table.TableName}: {{e}}')");
            sb.AppendLine();
        }

        // risk_summary CSV 自動検出
        sb.AppendLine("seed_files = _glob.glob('/lakehouse/default/Files/seed/*_risk_summary.csv')");
        sb.AppendLine("print(f'Found {len(seed_files)} risk_summary files')");
        sb.AppendLine("risk_dfs = []");
        sb.AppendLine("for csv_path in seed_files:");
        sb.AppendLine("    table_name = os.path.basename(csv_path).replace('.csv', '')");
        sb.AppendLine("    division_id = table_name.replace('_risk_summary', '')");
        sb.AppendLine("    try:");
        sb.AppendLine("        df = spark.read.format('csv').option('header', 'true').load(f'Files/seed/{table_name}.csv')");
        sb.AppendLine("        df = df.withColumn('_ingested_at', current_timestamp())");
        sb.AppendLine("        df = df.withColumn('metric_value', col('metric_value').cast('double'))");
        sb.AppendLine("        df = df.withColumn('division', lit(division_id))");
        sb.AppendLine("        df.drop('division').write.mode('overwrite').format('delta').saveAsTable(table_name)");
        sb.AppendLine("        risk_dfs.append(df)");
        sb.AppendLine("        print(f'  ✓ {table_name}: {df.count()} rows')");
        sb.AppendLine("    except Exception as e:");
        sb.AppendLine("        print(f'  ✗ {table_name}: {e}')");
        sb.AppendLine();
        sb.AppendLine("if risk_dfs:");
        sb.AppendLine("    from functools import reduce");
        sb.AppendLine("    integrated = reduce(lambda a, b: a.unionByName(b, allowMissingColumns=True), risk_dfs)");
        sb.AppendLine("    integrated.write.mode('overwrite').format('delta').saveAsTable('integrated_risk_summary')");
        sb.AppendLine("    print(f'  ✓ integrated_risk_summary: {integrated.count()} rows')");
        sb.AppendLine();

        // Phase 2: Silver → Gold
        sb.AppendLine("# " + new string('=', 60));
        sb.AppendLine("# Phase 2: Silver → Gold (集計テーブル作成)");
        sb.AppendLine("# " + new string('=', 60));
        sb.AppendLine("print('\\nPhase 2: Silver → Gold')");
        sb.AppendLine();
        sb.AppendLine("try:");
        sb.AppendLine("    df = spark.read.format('delta').table('integrated_risk_summary')");
        sb.AppendLine("    print(f'Source: integrated_risk_summary ({df.count()} rows)')");
        sb.AppendLine();
        sb.AppendLine("    revenue_metrics = ['売上高', '月次売上', 'revenue', 'gross_revenue']");
        sb.AppendLine("    cost_metrics = ['総コスト', 'コスト', 'cost', 'total_cost', '営業費用']");
        sb.AppendLine("    customer_metrics = ['顧客数', 'アクティブ顧客数', 'active_customers', 'customer_count', 'ユーザー数']");
        sb.AppendLine("    churn_metrics = ['解約率', '離脱率', 'churn_rate', 'churned']");
        sb.AppendLine();
        sb.AppendLine("    df = df.withColumn('metric_lower', lower(col('metric_name')))");
        sb.AppendLine();
        sb.AppendLine("    def extract_metric(df, metric_list, alias):");
        sb.AppendLine("        condition = None");
        sb.AppendLine("        for m in metric_list:");
        sb.AppendLine("            c = col('metric_lower').contains(m.lower())");
        sb.AppendLine("            condition = c if condition is None else (condition | c)");
        sb.AppendLine("        return df.filter(condition).groupBy('division', 'year_month') \\");
        sb.AppendLine("            .agg(avg('metric_value').alias(alias))");
        sb.AppendLine();
        sb.AppendLine("    rev = extract_metric(df, revenue_metrics, 'gross_revenue_jpy')");
        sb.AppendLine("    cost = extract_metric(df, cost_metrics, 'total_cost_jpy')");
        sb.AppendLine("    cust = extract_metric(df, customer_metrics, 'active_customer_count')");
        sb.AppendLine("    churn = extract_metric(df, churn_metrics, 'churn_rate')");
        sb.AppendLine();
        sb.AppendLine("    gold = rev.join(cost, ['division', 'year_month'], 'left') \\");
        sb.AppendLine("            .join(cust, ['division', 'year_month'], 'left') \\");
        sb.AppendLine("            .join(churn, ['division', 'year_month'], 'left')");
        sb.AppendLine();
        sb.AppendLine("    gold = gold.withColumn('gross_margin_jpy',");
        sb.AppendLine("        when(col('total_cost_jpy').isNotNull(),");
        sb.AppendLine("             col('gross_revenue_jpy') - col('total_cost_jpy')).otherwise(None))");
        sb.AppendLine("    gold = gold.withColumn('gross_margin_rate',");
        sb.AppendLine("        when((col('gross_revenue_jpy').isNotNull()) & (col('gross_revenue_jpy') != 0),");
        sb.AppendLine("             col('gross_margin_jpy') / col('gross_revenue_jpy')).otherwise(None))");
        sb.AppendLine();
        sb.AppendLine("    gold = gold.withColumn('active_customer_count', col('active_customer_count').cast('int'))");
        sb.AppendLine("    gold = gold.withColumn('gross_revenue_jpy', col('gross_revenue_jpy').cast('long'))");
        sb.AppendLine("    gold = gold.withColumn('total_cost_jpy', col('total_cost_jpy').cast('long'))");
        sb.AppendLine("    gold = gold.withColumn('gross_margin_jpy', col('gross_margin_jpy').cast('long'))");
        sb.AppendLine();
        sb.AppendLine("    gold.write.mode('overwrite').format('delta').saveAsTable('kpi_monthly_revenue')");
        sb.AppendLine("    print(f'  ✓ kpi_monthly_revenue: {gold.count()} rows')");
        sb.AppendLine("except Exception as e:");
        sb.AppendLine("    print(f'  ✗ kpi_monthly_revenue: FAILED - {e}')");
        sb.AppendLine();

        // 事業部別分析テーブル
        sb.AppendLine("try:");
        sb.AppendLine("    df = spark.read.format('delta').table('integrated_risk_summary')");
        sb.AppendLine("    divisions = [r.division for r in df.select('division').distinct().collect()]");
        sb.AppendLine("    for div in divisions:");
        sb.AppendLine("        div_df = df.filter(col('division') == div).drop('division')");
        sb.AppendLine("        table_name = f'{div}_ai_risk_summary'");
        sb.AppendLine("        div_df.write.mode('overwrite').format('delta').saveAsTable(table_name)");
        sb.AppendLine("        print(f'  ✓ {table_name}: {div_df.count()} rows')");
        sb.AppendLine("except Exception as e:");
        sb.AppendLine("    print(f'  ✗ division_ai tables: FAILED - {e}')");
        sb.AppendLine();
        sb.AppendLine("print('\\n✅ Medallion ETL Complete')");

        return sb.ToString();
    }

    // Legacy methods kept for reference but no longer called
    private static string BuildBronzeToSilverCode(AnalysisResult analysis, LakehouseInfo bronze, LakehouseInfo silver)
        => BuildFullMedallionEtlCode(analysis);

    private static string BuildSilverToGoldCode(AnalysisResult analysis, LakehouseInfo silver, LakehouseInfo gold)
        => "";

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
        // --verbose を付加して response headers (x-ms-operation-id) を stderr に出力させる
        var fullArgs = azArgs.Contains("--verbose") ? azArgs : azArgs + " --verbose";
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "az",
            Arguments = fullArgs,
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
            // INFO: 行（verbose出力）を除外して実際のエラーメッセージを抽出
            var combinedLines = (stderr + "\n" + stdout)
                .Split('\n')
                .Where(l => !l.TrimStart().StartsWith("INFO:"))
                .ToArray();
            var filtered = string.Join("\n", combinedLines).Trim();
            var errorDetail = string.IsNullOrWhiteSpace(filtered)
                ? (stderr + "\n" + stdout)[..Math.Min(800, (stderr + "\n" + stdout).Length)]
                : filtered[..Math.Min(800, filtered.Length)];
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
