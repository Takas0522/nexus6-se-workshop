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

        var systemContext = """
            あなたはビジネスシステム分析の専門家です。
            ニュースの内容をベースにビジネスインパクトを診断し、各業務領域の部署に通知を行うシステムを構築します。

            既存のデータベース構成として以下のスキーマが存在します:
            - sqldb_common_01: 統合顧客DB (unified_customers, domain_id_mappings, customer_segments等)
            - sqldb_mobile_01~05: モバイル事業 (customers, contracts, usage_billing, cost_items, mnp_history等)
            - sqldb_ecommerce_01~05: EC事業 (products, orders, inventory, point_campaigns, behaviors等)
            - sqldb_fintech_01~05: 金融事業 (accounts, card_transactions, fx_positions, credit_reviews, loan_balances等)

            3つのニュースシナリオを想定します:
            1. 為替急変シナリオ
            2. 競合統合シナリオ
            3. 金融政策転換シナリオ
            """;

        var prompt = $$"""
            以下の業務領域に基づいて、ニュース分析・インパクト診断システムのデータモデルを分析してください。

            業務領域: {{string.Join(", ", assessment.Domains)}}
            従業員数: {{assessment.EmployeeCount}}名
            想定顧客数: {{assessment.EmployeeCount * 3}}名 (従業員数×3)

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

            データ量の計算:
            - 顧客数 = {{assessment.EmployeeCount}} × 3 = {{assessment.EmployeeCount * 3}}
            - {{assessment.Domains[0]}}月間トランザクション: 顧客数 × 2
            - {{assessment.Domains[1]}}月間トランザクション: 顧客数 × 5
            - {{assessment.Domains[2]}}月間トランザクション: 顧客数 × 3
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
