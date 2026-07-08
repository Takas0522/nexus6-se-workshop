using System.Text;
using System.Text.Json;
using EnvironmentSetup.App.Models;
using EnvironmentSetup.App.Services;

namespace EnvironmentSetup.App.Steps;

/// <summary>
/// ステップ12: Foundry Knowledge アップロード
/// - Step11 で生成した Skill/DS.md を Foundry Files API でアップロード
/// - Vector Store を作成しファイルを紐付け
/// - Assistants を作成/更新して tool_resources に Vector Store を設定
/// </summary>
public class Step12FoundryKnowledge : ISetupStep
{
    private readonly AzureCliWrapper _az;

    public int StepNumber => 12;
    public string Name => "Foundry Knowledge アップロード";

    public Step12FoundryKnowledge(AzureCliWrapper az)
    {
        _az = az;
    }

    public async Task ExecuteAsync(SetupState state, CancellationToken ct = default)
    {
        var deployment = state.Deployment
            ?? throw new InvalidOperationException("デプロイが未完了です。Step 5 を先に実行してください。");

        if (string.IsNullOrWhiteSpace(deployment.FoundryEndpoint) && string.IsNullOrWhiteSpace(deployment.FoundryProjectEndpoint))
            throw new InvalidOperationException("Foundry エンドポイントが未設定です。");

        var skillDir = Path.GetFullPath("./output/skills");
        var dsDir = Path.Combine(skillDir, "ds");

        if (!Directory.Exists(skillDir))
            throw new InvalidOperationException($"Skill/DS.md ディレクトリが見つかりません: {skillDir}\nStep 11 を先に実行してください。");

        // 全 API をアカウントエンドポイント + cognitiveservices スコープで統一
        // (project endpoint では Vector Store と Assistants が別空間になるため)
        var endpoint = deployment.FoundryEndpoint;
        var apiVersion = "2024-10-01-preview";

        Console.WriteLine($"  Foundry: {endpoint}");
        Console.WriteLine($"  Knowledge Dir: {skillDir}\n");

        // 0. RBAC 伝播待機 (Files API に GET して 401 でなくなるまで待つ)
        Console.WriteLine("  🔑 RBAC 伝播確認中 (最大10分)...");
        await WaitForRbacPropagationAsync(endpoint, apiVersion, ct);
        Console.WriteLine("    ✓ RBAC OK\n");

        // 1. ファイルアップロード
        Console.WriteLine("  📤 Foundry Files API でファイルをアップロード中...");
        var fileIds = new List<string>();

        var mdFiles = Directory.GetFiles(skillDir, "*.md", SearchOption.AllDirectories);
        foreach (var filePath in mdFiles)
        {
            var fileName = Path.GetFileName(filePath);
            Console.Write($"    {fileName}...");

            var fileId = await UploadFileAsync(endpoint, apiVersion, filePath, fileName, ct);
            if (!string.IsNullOrEmpty(fileId))
            {
                fileIds.Add(fileId);
                Console.WriteLine($" ✓ ({fileId})");
            }
            else
            {
                Console.WriteLine(" ⚠️ スキップ");
            }
        }

        if (fileIds.Count == 0)
        {
            throw new InvalidOperationException(
                "ファイルが1件もアップロードできませんでした。RBAC (Cognitive Services User) が伝播済みか確認し、再実行してください。");
        }

        Console.WriteLine($"\n  📦 {fileIds.Count} ファイルアップロード完了");

        // 2. Vector Store 作成
        Console.WriteLine("\n  🗄️ Vector Store を作成中...");
        var vectorStoreId = await CreateVectorStoreAsync(endpoint, apiVersion, fileIds, ct);
        Console.WriteLine($"    Vector Store ID: {vectorStoreId}");

        // 3. Vector Store のインデックス完了待ち
        Console.WriteLine("    インデックス作成待ち中...");
        await WaitForVectorStoreReadyAsync(endpoint, apiVersion, vectorStoreId, ct);
        Console.WriteLine("    ✓ Vector Store ready");

        // 4. Assistants 作成 (アカウントエンドポイント + cognitiveservices スコープ)
        Console.WriteLine("\n  🤖 Assistants を作成中...");

        var assistants = GetAssistantDefinitions(vectorStoreId);
        var createdAssistants = new Dictionary<string, string>();

        foreach (var (name, instructions) in assistants)
        {
            Console.Write($"    {name}...");
            var assistantId = await CreateOrUpdateAssistantAsync(
                endpoint, apiVersion, name, instructions, vectorStoreId, ct);
            createdAssistants[name] = assistantId;
            Console.WriteLine($" ✓ ({assistantId})");
        }

        // 5. 状態保存
        deployment.FoundryVectorStoreId = vectorStoreId;
        deployment.FoundryAssistantIds = createdAssistants;

        Console.WriteLine($"\n  ✓ Foundry Knowledge セットアップ完了");
        Console.WriteLine($"    Vector Store: {vectorStoreId}");
        Console.WriteLine($"    Assistants: {createdAssistants.Count} 個作成");
    }

    /// <summary>
    /// Step12 開始前に RBAC が伝播しているか確認。GET /files で 401 以外が返るまで待機。
    /// </summary>
    private async Task WaitForRbacPropagationAsync(string endpoint, string apiVersion, CancellationToken ct)
    {
        // GET /files は権限不足でも 200 (空リスト) を返す場合があるため、
        // 実際に POST /files でダミーファイルをアップロードして確認する
        const int maxAttempts = 20; // 20 × 30s = 10分
        const int delaySec = 30;

        var dummyContent = "RBAC probe test"u8.ToArray();

        for (int i = 1; i <= maxAttempts; i++)
        {
            var token = await GetCognitiveServicesTokenAsync();
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            using var form = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(dummyContent);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            form.Add(fileContent, "file", "_rbac_probe.txt");
            form.Add(new StringContent("assistants"), "purpose");

            var url = $"{BuildApiBase(endpoint)}/files?api-version={apiVersion}";
            try
            {
                var response = await httpClient.PostAsync(url, form, ct);
                if ((int)response.StatusCode != 401 && (int)response.StatusCode != 403)
                {
                    // 成功した場合はプローブファイルを削除
                    if (response.IsSuccessStatusCode)
                    {
                        try
                        {
                            var body = await response.Content.ReadAsStringAsync(ct);
                            using var doc = JsonDocument.Parse(body);
                            var fileId = doc.RootElement.GetProperty("id").GetString();
                            if (!string.IsNullOrEmpty(fileId))
                            {
                                var delUrl = $"{BuildApiBase(endpoint)}/files/{fileId}?api-version={apiVersion}";
                                _ = httpClient.DeleteAsync(delUrl, ct);
                            }
                        }
                        catch { /* cleanup best-effort */ }
                    }
                    return; // RBAC OK
                }
                Console.Write($"    [{i}/{maxAttempts}] 401 → {delaySec}s 待機...\n");
            }
            catch (Exception ex)
            {
                Console.Write($"    [{i}/{maxAttempts}] {ex.GetType().Name} → {delaySec}s 待機...\n");
            }

            await Task.Delay(TimeSpan.FromSeconds(delaySec), ct);
        }

        throw new InvalidOperationException(
            "RBAC (Cognitive Services User) が10分経過しても伝播しませんでした。" +
            "Azure Portal で AI Services の IAM を確認してください。");
    }

    private async Task<string> UploadFileAsync(
        string projectEndpoint, string apiVersion, string filePath, string fileName, CancellationToken ct)
    {
        // RBAC は事前に WaitForRbacPropagationAsync で確認済み。短めのリトライのみ。
        const int maxRetries = 3;
        const int retryDelaySec = 10;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            var token = await GetCognitiveServicesTokenAsync();
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            using var form = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(await File.ReadAllBytesAsync(filePath, ct));
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            form.Add(fileContent, "file", fileName);
            form.Add(new StringContent("assistants"), "purpose");

            var url = $"{BuildApiBase(projectEndpoint)}/files?api-version={apiVersion}";
            var response = await httpClient.PostAsync(url, form, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(body);
                return doc.RootElement.GetProperty("id").GetString() ?? "";
            }

            if ((int)response.StatusCode == 401 && attempt < maxRetries)
            {
                Console.Write($" [401→{retryDelaySec}s待機]");
                await Task.Delay(TimeSpan.FromSeconds(retryDelaySec), ct);
                continue;
            }

            Console.Write($" [Error {(int)response.StatusCode}]");
            return "";
        }

        return "";
    }

    private async Task<string> CreateVectorStoreAsync(
        string projectEndpoint, string apiVersion, List<string> fileIds, CancellationToken ct)
    {
        var token = await GetCognitiveServicesTokenAsync();
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var payload = JsonSerializer.Serialize(new
        {
            name = "nexus6-knowledge-base",
            file_ids = fileIds
        });

        var url = $"{BuildApiBase(projectEndpoint)}/vector_stores?api-version={apiVersion}";
        var response = await httpClient.PostAsync(url,
            new StringContent(payload, Encoding.UTF8, "application/json"), ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("Vector Store ID not returned");
    }

    private async Task WaitForVectorStoreReadyAsync(
        string projectEndpoint, string apiVersion, string vectorStoreId, CancellationToken ct)
    {
        var token = await GetCognitiveServicesTokenAsync();
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var url = $"{BuildApiBase(projectEndpoint)}/vector_stores/{vectorStoreId}?api-version={apiVersion}";

        for (var i = 0; i < 180; i++) // max 15 minutes (50ファイルのインデックスに余裕を持たせる)
        {
            await Task.Delay(5000, ct);
            var response = await httpClient.GetAsync(url, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(body);
                var status = doc.RootElement.GetProperty("status").GetString();
                Console.Write(i % 12 == 0 && i > 0 ? $" [{status}]" : ".");
                if (status == "completed") { Console.WriteLine(" ✓"); return; }
                if (status == "failed")
                    throw new InvalidOperationException("Vector Store indexing failed");
            }
        }

        throw new TimeoutException("Vector Store indexing timed out (15 minutes)");
    }

    private async Task<string> CreateOrUpdateAssistantAsync(
        string projectEndpoint, string apiVersion, string name, string instructions,
        string vectorStoreId, CancellationToken ct)
    {
        var token = await GetCognitiveServicesTokenAsync();
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // 既存 Assistant を検索
        var listUrl = $"{BuildApiBase(projectEndpoint)}/assistants?api-version={apiVersion}";
        var listResponse = await httpClient.GetAsync(listUrl, ct);
        var listBody = await listResponse.Content.ReadAsStringAsync(ct);
        string? existingId = null;

        if (listResponse.IsSuccessStatusCode)
        {
            using var listDoc = JsonDocument.Parse(listBody);
            foreach (var a in listDoc.RootElement.GetProperty("data").EnumerateArray())
            {
                if (a.GetProperty("name").GetString() == name)
                {
                    existingId = a.GetProperty("id").GetString();
                    break;
                }
            }
        }

        var payload = JsonSerializer.Serialize(new
        {
            name,
            model = "gpt-5",
            instructions,
            tools = new[] { new { type = "file_search" } },
            tool_resources = new
            {
                file_search = new
                {
                    vector_store_ids = new[] { vectorStoreId }
                }
            }
        });

        if (existingId != null)
        {
            // Update existing
            var updateUrl = $"{BuildApiBase(projectEndpoint)}/assistants/{existingId}?api-version={apiVersion}";
            var updateResponse = await httpClient.PostAsync(updateUrl,
                new StringContent(payload, Encoding.UTF8, "application/json"), ct);
            updateResponse.EnsureSuccessStatusCode();
            return existingId;
        }
        else
        {
            // Create new
            var createUrl = $"{BuildApiBase(projectEndpoint)}/assistants?api-version={apiVersion}";
            var createResponse = await httpClient.PostAsync(createUrl,
                new StringContent(payload, Encoding.UTF8, "application/json"), ct);
            var createBody = await createResponse.Content.ReadAsStringAsync(ct);
            createResponse.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(createBody);
            return doc.RootElement.GetProperty("id").GetString()
                ?? throw new InvalidOperationException("Assistant ID not returned");
        }
    }

    private static IReadOnlyList<(string Name, string Instructions)> GetAssistantDefinitions(string vectorStoreId)
    {
        return new[]
        {
            ("nexus6-business-impact-filesearch", """
                あなたはビジネスインパクト評価を担当するエージェントです。
                提供されたニュース本文、Web調査結果、Fabric Gold KPI、Skill.md / DS.md の抜粋を照合し、
                モバイル通信・Eコマース・Fintechの各事業部への影響を0〜5のスコアで評価してください。
                Foundry file_search で Skill.md / DS.md を必ず検索し、閾値・過去事例・KPI定義を参照してください。
                出力はJSON形式で返してください。
                """),
            ("nexus6-web-research", """
                あなたはニュース分析の調査担当エージェントです。
                入力されたニュース本文を読み、Web検索を行い背景情報を補完してください。
                file_search で DS.md を参照し、関連するデータソース情報を確認してください。
                出力はJSON形式（summary / key_factors / source_urls）で返してください。
                """),
            ("nexus6-mobile-recommend-filesearch", """
                あなたはモバイル通信事業部のレコメンドエージェントです。
                ニュースとビジネスインパクト評価に基づき、モバイル事業部が取るべきアクションを提案してください。
                file_search で mobile_skill_*.md を検索し、過去事例や閾値を参照してください。
                """),
            ("nexus6-ecommerce-recommend-filesearch", """
                あなたはEコマース事業部のレコメンドエージェントです。
                ニュースとビジネスインパクト評価に基づき、Eコマース事業部が取るべきアクションを提案してください。
                file_search で ecommerce_skill_*.md を検索し、過去事例や閾値を参照してください。
                """),
            ("nexus6-fintech-recommend-filesearch", """
                あなたはFintech事業部のレコメンドエージェントです。
                ニュースとビジネスインパクト評価に基づき、Fintech事業部が取るべきアクションを提案してください。
                file_search で fintech_skill_*.md を検索し、過去事例や閾値を参照してください。
                """),
            ("nexus6-division-recommend-filesearch", """
                あなたは汎用事業部レコメンドエージェントです。
                ニュースとビジネスインパクト評価に基づき、担当事業部が取るべきアクションを提案してください。
                file_search で skill_*.md を検索し、過去事例や閾値を参照してください。
                """),
        };
    }

    /// <summary>
    /// プロジェクトエンドポイントの場合は /openai/ を付けない。
    /// アカウントレベルエンドポイントの場合は /openai/ を付加する。
    /// </summary>
    private static string BuildApiBase(string endpoint)
    {
        var trimmed = endpoint.TrimEnd('/');
        // プロジェクトエンドポイント (*.services.ai.azure.com/api/projects/...) は /openai/ 不要
        if (trimmed.Contains("/api/projects/", StringComparison.OrdinalIgnoreCase))
            return trimmed;
        return $"{trimmed}/openai";
    }

    private async Task<string> GetFoundryTokenAsync()
    {
        var result = await _az.RunAsync(
            "account get-access-token --resource https://ai.azure.com --query accessToken -o tsv",
            silent: true);
        return result.Trim();
    }

    private async Task<string> GetCognitiveServicesTokenAsync()
    {
        var result = await _az.RunAsync(
            "account get-access-token --resource https://cognitiveservices.azure.com --query accessToken -o tsv",
            silent: true);
        return result.Trim();
    }
}
