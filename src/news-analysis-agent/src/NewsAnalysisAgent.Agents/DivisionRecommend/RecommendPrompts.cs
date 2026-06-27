using NewsAnalysisAgent.Models;

namespace NewsAnalysisAgent.Agents.DivisionRecommend;

public static class RecommendPrompts
{
    private const string CommonSystemPrompt = """
        あなたは事業部別レコメンドエージェントです。
        ニュース本文、Web調査、ビジネスインパクト評価、事業部KPI、Skill/DSを照合し、対象事業部がすぐ実行できる Next Action を最大5件生成してください。
        Foundry file_search で Skill.md / DS.md を必ず検索し、閾値・過去事例・KPI定義を反映してください。
        根拠には引用元ファイル名を source_files に格納してください。
        Fabric KPI を語る際は必ず DS.md の論理項目（日本語）で記述し、物理列名は headline / next_actions に出さないでください。
        数値は 3 桁区切り、不要小数は丸め、単位は DS.md の定義に従って日本語（円、件、% など）で付与してください。
        出力は必ず JSON オブジェクトのみとし、指定スキーマ以外のフィールドを追加しないでください。
        data_references には参照した Fabric Gold テーブル名、KPI 名、Skill.md / DS.md のファイル名を含めてください。
        source_files には参照した Skill.md のファイル名のみを配列で含めてください。
        kpi_references には参照した Fabric KPI の物理列名、値、単位、テーブル名を含めてください。
        各 next_action は title（30 字以内の要点）と body（具体施策と数値根拠）を分けて返してください。
        URL は確実な実在参照のみを出力し、不確実な URL や example.com は出力しないでください。
        """;

    public const string OutputJsonSchema = """
        {
          "division": "mobile | ecommerce | fintech",
          "headline": "300文字以内の要約",
          "next_actions": [
            { "title": "30字以内の要点", "body": "具体施策と数値根拠" }
          ],
          "data_references": ["根拠データ参照"],
          "source_files": ["mobile_skill_competitor-mnp.md"],
          "kpi_references": [
            { "physical_name": "mnp_out_rate", "value": "4.1", "unit": "%", "table": "mobile_ai.risk_summary" }
          ]
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
