using System.Text.Json;
using System.Text.Json.Serialization;
using EnvironmentSetup.App.Models;
using EnvironmentSetup.App.Services;

namespace EnvironmentSetup.App.Steps;

/// <summary>
/// ステップ9: ドメイン設定アップロード - Assessment の業務領域から domain-config.json を生成し Blob にアップロード。
/// Hosted Agent はこの Blob から DivisionsConfig を読み込み、動的に事業部ステップを構築する。
/// </summary>
public class Step09DomainConfigUpload : ISetupStep
{
    private readonly AzureCliWrapper _az;

    public int StepNumber => 9;
    public string Name => "ドメイン設定アップロード";

    public Step09DomainConfigUpload(AzureCliWrapper az)
    {
        _az = az;
    }

    public async Task ExecuteAsync(SetupState state, CancellationToken ct = default)
    {
        var assessment = state.Assessment
            ?? throw new InvalidOperationException("アセスメントが未完了です。Step 1 を先に実行してください。");
        var deployment = state.Deployment
            ?? throw new InvalidOperationException("デプロイが未完了です。Step 5 を先に実行してください。");

        Console.WriteLine("  ドメイン設定 (domain-config.json) を生成中...\n");

        // 1. Assessment.Domains から DivisionsConfig を生成
        var divisions = new List<DomainConfigEntry>();
        for (var i = 0; i < assessment.Domains.Count; i++)
        {
            var domain = assessment.Domains[i];
            var divisionId = NormalizeDivisionId(domain);
            var channel = i < assessment.NotificationChannels.Count
                ? assessment.NotificationChannels[i]
                : $"{divisionId}-alerts";

            divisions.Add(new DomainConfigEntry
            {
                Id = divisionId,
                Label = domain,
                InterestAreas = GenerateInterestAreas(domain),
                FabricTable = $"{divisionId}_ai.risk_summary",
                TeamsTeamId = "",
                TeamsChannelId = "",
                TeamsWorkflowsUrl = "",
                FoundryAssistantName = $"nexus6-{divisionId}-recommend-filesearch"
            });
        }

        var config = new { Divisions = divisions };
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        // 2. ローカルに保存
        var outputDir = Path.GetFullPath("./output");
        Directory.CreateDirectory(outputDir);
        var configPath = Path.Combine(outputDir, "domain-config.json");
        await File.WriteAllTextAsync(configPath, json, ct);
        Console.WriteLine($"    ✓ 生成完了: {configPath}");

        // 3. 各事業部の設定を表示
        foreach (var div in divisions)
        {
            Console.WriteLine($"    📋 {div.Label} ({div.Id})");
            Console.WriteLine($"       テーブル: {div.FabricTable}");
            Console.WriteLine($"       関心領域: {string.Join(", ", div.InterestAreas)}");
        }

        // 4. Blob にアップロード
        var storageAccount = deployment.StorageAccountSkills;
        if (!string.IsNullOrEmpty(storageAccount))
        {
            Console.WriteLine($"\n    Blob にアップロード中 ({storageAccount}/config/domain-config.json)...");
            try
            {
                try
                {
                    await _az.RunAsync(
                        $"storage blob upload --account-name {storageAccount} " +
                        $"--container-name config --name domain-config.json " +
                        $"--file {configPath} --overwrite --auth-mode login",
                        silent: true);
                    Console.WriteLine("    ✓ Blob アップロード完了");
                }
                catch
                {
                    // RBAC 未伝播の場合はアカウントキーでフォールバック
                    Console.WriteLine("    ⚠️ login 認証失敗 → アカウントキーで再試行...");
                    await _az.RunAsync(
                        $"storage blob upload --account-name {storageAccount} " +
                        $"--container-name config --name domain-config.json " +
                        $"--file {configPath} --overwrite --auth-mode key",
                        silent: true);
                    Console.WriteLine("    ✓ Blob アップロード完了 (key mode)");
                }

                // Blob URI を表示
                var blobUri = $"https://{storageAccount}.blob.core.windows.net/config/domain-config.json";
                Console.WriteLine($"    📍 Blob URI: {blobUri}");
                Console.WriteLine($"\n    ℹ️  Hosted Agent の環境変数に設定してください:");
                Console.WriteLine($"       DomainConfig__BlobUri={blobUri}");

                state.DomainConfigBlobUri = blobUri;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    ⚠️ Blob アップロード失敗: {ex.Message}");
                Console.WriteLine("    手動でアップロードしてください:");
                Console.WriteLine($"    az storage blob upload --account-name {storageAccount} " +
                    $"--container-name config --name domain-config.json --file {configPath} --auth-mode key");
            }
        }
        else
        {
            Console.WriteLine("\n    ⚠️ Storage Account が未設定です。手動でアップロードしてください。");
        }

        Console.WriteLine($"\n  ✓ ドメイン設定生成完了 ({divisions.Count} 事業部)");
    }

    private static string NormalizeDivisionId(string domain)
    {
        // 日本語の業務領域名を英語IDに変換
        var normalized = domain.ToLowerInvariant().Trim();
        return normalized switch
        {
            "モバイル通信" or "モバイル" or "mobile" => "mobile",
            "eコマース" or "ec" or "ecommerce" or "ＥＣ" => "ecommerce",
            "フィンテック" or "fintech" or "金融" => "fintech",
            "物流" or "logistics" => "logistics",
            "製造" or "manufacturing" => "manufacturing",
            "小売" or "retail" => "retail",
            "hr" or "人事" => "hr",
            "マーケティング" or "marketing" => "marketing",
            _ => domain.ToLowerInvariant()
                .Replace(" ", "_").Replace("　", "_")
                .Replace("・", "_")
        };
    }

    private static List<string> GenerateInterestAreas(string domain)
    {
        var normalized = domain.ToLowerInvariant().Trim();
        return normalized switch
        {
            "モバイル通信" or "モバイル" => ["端末コスト", "MNP転出率", "分割払い残高", "解約問い合わせ", "施策配信履歴"],
            "eコマース" or "ec" => ["在庫", "越境EC仕入コスト", "ポイント還元", "会員行動", "キャンペーンROI"],
            "フィンテック" or "金融" => ["FXポジション", "海外カード決済", "ローン残高", "延滞率", "与信審査"],
            "物流" => ["配送コスト", "倉庫稼働率", "返品率", "配送リードタイム", "在庫回転率"],
            "製造" => ["生産効率", "不良率", "原材料コスト", "設備稼働率", "納期遵守率"],
            "小売" => ["客単価", "来店数", "在庫回転", "廃棄ロス", "リピート率"],
            "人事" or "hr" => ["離職率", "採用コスト", "残業時間", "エンゲージメント", "研修効果"],
            _ => ["事業KPI", "コスト指標", "リスク指標", "顧客指標", "収益指標"]
        };
    }
}

internal class DomainConfigEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("interestAreas")]
    public List<string> InterestAreas { get; set; } = [];

    [JsonPropertyName("fabricTable")]
    public string FabricTable { get; set; } = string.Empty;

    [JsonPropertyName("teamsTeamId")]
    public string TeamsTeamId { get; set; } = string.Empty;

    [JsonPropertyName("teamsChannelId")]
    public string TeamsChannelId { get; set; } = string.Empty;

    [JsonPropertyName("teamsWorkflowsUrl")]
    public string TeamsWorkflowsUrl { get; set; } = string.Empty;

    [JsonPropertyName("foundryAssistantName")]
    public string FoundryAssistantName { get; set; } = string.Empty;
}
