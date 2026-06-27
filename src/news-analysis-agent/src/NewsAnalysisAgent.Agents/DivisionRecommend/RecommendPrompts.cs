using NewsAnalysisAgent.Models;

namespace NewsAnalysisAgent.Agents.DivisionRecommend;

public static class RecommendPrompts
{
    private const string CommonSystemPrompt = """
        あなたは事業部別レコメンドエージェントです。
        ニュース本文、Web調査、ビジネスインパクト評価、事業部KPI、Skill/DSを照合し、対象事業部がすぐ実行できる Next Action を最大5件生成してください。
        出力は必ず JSON オブジェクトのみとし、指定スキーマ以外のフィールドを追加しないでください。
        data_references には参照した Fabric Gold テーブル名、KPI 名、Skill/DS 名を含めてください。
        """;

    public const string OutputJsonSchema = """
        {
          "division": "mobile | ecommerce | fintech",
          "headline": "300文字以内の要約",
          "next_actions": ["実行アクション"],
          "data_references": ["根拠データ参照"]
        }
        """;

    public static string SystemPrompt(DivisionKind division) => $"""
        {CommonSystemPrompt}

        対象事業部: {DivisionLabel(division)}
        関心領域:
        {InterestAreas(division)}
        """;

    public static string DivisionToken(DivisionKind division) => division switch
    {
        DivisionKind.Mobile => "mobile",
        DivisionKind.Ecommerce => "ecommerce",
        DivisionKind.Fintech => "fintech",
        _ => division.ToString().ToLowerInvariant()
    };

    private static string DivisionLabel(DivisionKind division) => division switch
    {
        DivisionKind.Mobile => "モバイル通信",
        DivisionKind.Ecommerce => "Eコマース",
        DivisionKind.Fintech => "Fintech",
        _ => division.ToString()
    };

    private static string InterestAreas(DivisionKind division) => division switch
    {
        DivisionKind.Mobile => "- 端末コスト、MNP 転出率、分割払い残高、解約問い合わせ、施策配信履歴",
        DivisionKind.Ecommerce => "- 在庫、越境 EC 仕入コスト、ポイント還元、会員行動、キャンペーン ROI",
        DivisionKind.Fintech => "- FX ポジション、海外カード決済、ローン残高、延滞率、与信審査",
        _ => "- 事業部 KPI とリスクサマリ"
    };
}
