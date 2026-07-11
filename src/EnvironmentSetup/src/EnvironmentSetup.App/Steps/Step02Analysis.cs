using System.Text.Json;
using EnvironmentSetup.App.Models;
using EnvironmentSetup.App.Services;

namespace EnvironmentSetup.App.Steps;

/// <summary>
/// ステップ2: 分析 - 業務情報を分析しアプリ構成に必要なデータモデルを決定
/// </summary>
public class Step02Analysis : ISetupStep
{
    private readonly CopilotService _copilot;

    public int StepNumber => 2;
    public string Name => "業務分析";

    public Step02Analysis(CopilotService copilot)
    {
        _copilot = copilot;
    }

    public async Task ExecuteAsync(SetupState state, CancellationToken ct = default)
    {
        var assessment = state.Assessment
            ?? throw new InvalidOperationException("アセスメントが未完了です。Step 1 を先に実行してください。");

        Console.WriteLine("  Copilot SDK を使用して業務分析を実行中...\n");

        var domains = string.Join(", ", assessment.Domains);

        var systemContext = $"""
            あなたはビジネスシステム分析の専門家です。
            ニュースの内容をベースにビジネスインパクトを診断し、各業務領域の部署に通知を行うシステムを構築します。

            対象業務領域: {domains}

            各業務領域ごとにデータベースが存在し、顧客・契約・取引・在庫等のテーブルを保持しています。
            統合顧客DB (sqldb_common_01) には unified_customers, domain_id_mappings, customer_segments が存在します。

            ニュースシナリオは一般的な時事ニュース・経済ニュースとして自然な記事を3件考案してください。
            各ニュースは具体的な数値・企業名・地域・日付などを含むリアルな報道記事のように記述し、
            「○○事業に影響」のような事業部への直接的な影響言及は含めないでください。
            影響分析はAIエージェントが行うため、ニュース自体は中立的な報道として記述します。
            """;

        var totalCustomers = assessment.EmployeeCount * 3;
        var domainList = assessment.Domains.ToList();
        var txLines = string.Join("\n", domainList.Select((d, i) =>
            $"            - {d}月間トランザクション: 顧客数 × {(i == 1 ? 5 : i == 2 ? 3 : 2)}"));

        var prompt = $$"""
            以下の業務領域に基づいて、ニュース分析・インパクト診断システムのデータモデルを分析してください。

            業務領域: {{domains}}
            従業員数: {{assessment.EmployeeCount}}名
            想定顧客数: {{totalCustomers}}名 (従業員数×3)

            以下をJSON形式で出力してください:
            {
              "tables": [
                {"database": "DB名", "tableName": "テーブル名", "description": "説明", "estimatedRows": 数値, "columns": [{"name": "列名", "type": "型", "nullable": bool, "description": "説明"}]}
              ],
              "estimatedDataCounts": {
                "totalCustomers": 数値,
                "monthlyTransactions": {"領域1": 月間件数, "領域2": 月間件数, "領域3": 月間件数},
                "totalMonths": 6,
                "totalRecords": 合計レコード数
              },
              "newsScenarios": [
                {"id": "ID", "title": "タイトル", "summary": "概要", "category": "カテゴリ", "impactDomains": ["領域"]}
              ],
              "notificationTemplates": [
                {"domain": "領域", "newsScenarioId": "シナリオID", "channel": "チャネル", "title": "通知タイトル", "body": "通知本文"}
              ]
            }

            newsScenarios は一般的な時事ニュース・経済ニュースとして自然な記事を3件考案してください。
            具体的な数値・企業名・地域・日付を含むリアルな報道記事として記述し、
            「○○事業に影響」のような事業部への直接的な言及は含めないでください。
            各シナリオの impactDomains には上記業務領域の中から影響を受ける領域を選んでください（システム内部用、記事には表示されません）。

            データ量の計算:
            - 顧客数 = {{assessment.EmployeeCount}} × 3 = {{totalCustomers}}
            {{txLines}}
            - 期間: 6ヶ月分

            JSONのみを出力してください。説明文は不要です。
            """;

        var response = await _copilot.GenerateWithContextAsync(systemContext, prompt);

        // JSON部分を抽出
        var jsonStart = response.IndexOf('{');
        var jsonEnd = response.LastIndexOf('}');
        if (jsonStart < 0 || jsonEnd < 0)
            throw new InvalidOperationException("分析結果のJSONパースに失敗しました。");

        var json = response[jsonStart..(jsonEnd + 1)];
        var result = JsonSerializer.Deserialize<AnalysisResult>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("分析結果のデシリアライズに失敗しました。");

        state.Analysis = result;

        Console.WriteLine($"  ✓ テーブル定義: {result.Tables.Count}件");
        Console.WriteLine($"  ✓ ニュースシナリオ: {result.NewsScenarios.Count}件");
        Console.WriteLine($"  ✓ 通知テンプレート: {result.NotificationTemplates.Count}件");
        Console.WriteLine($"  ✓ 想定総レコード数: {result.EstimatedDataCounts.TotalRecords:N0}件");
    }
}
