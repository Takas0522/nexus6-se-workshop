using NewsAnalysisAgent.Models;

namespace NewsAnalysisAgent.Agents.BusinessImpact;

public static class BusinessImpactPrompts
{
    public static string BuildSystemPrompt(IReadOnlyList<DivisionConfig> divisions)
    {
        var divisionNames = string.Join("・", divisions.Select(d => d.Label));
        return $"""
あなたはビジネスインパクト評価を担当するエージェントです。
提供されたニュース本文、Web調査結果、Fabric Gold KPI、Skill.md / DS.md の抜粋を照合し、
{divisionNames}の各事業部への影響を0〜5のスコアで評価してください。
Foundry file_search で Skill.md / DS.md を必ず検索し、閾値・過去事例・KPI定義を根拠に含めてください。
根拠には引用元ファイル名を source_files に格納してください。
Fabric KPI を語る際は必ず DS.md の論理項目（日本語）で記述し、物理列名は本文・理由文に出さないでください。
数値は 3 桁区切り、不要小数は丸め、単位は DS.md の定義に従って日本語（円、件、% など）で付与してください。

参照可能な内部データ:
- 全社売上・コスト・粗利（FabricDataPlugin 経由）
- 事業部別 KPI 推移（FabricDataPlugin 経由）
- Skill.md / DS.md の判断ロジック・KPI定義

評価軸:
- 収益影響
- コスト影響
- 顧客影響
- 競争環境影響

必ず JSON オブジェクトのみを返してください。説明文や Markdown は含めないでください。
impact_scores の division には必ず次のID文字列を使用してください: {string.Join(", ", divisions.Select(d => d.Id))}
priority_order は impact_scores を score 降順で並べた division リストにしてください。
data_references には参照した Skill.md / DS.md のファイル名と Fabric KPI 名を含めてください。
source_files には参照した Skill.md のファイル名のみを配列で含めてください。
kpi_references には参照した Fabric KPI の物理列名、値、単位、テーブル名を含めてください。
""";
    }

    public static string BuildOutputJsonSchema(IReadOnlyList<DivisionConfig> divisions)
    {
        var scoresJson = string.Join(",\n    ", divisions.Select(d => $$"""{ "division": "{{d.Id}}", "score": 0.0, "risk_level": "low" }"""));
        var reasonsJson = string.Join(",\n    ", divisions.Select(d => $"\"{d.Id}: reason\""));
        var orderJson = string.Join(", ", divisions.Select(d => $"\"{d.Id}\""));
        return $$"""
{
  "impact_scores": [
    {{scoresJson}}
  ],
  "impact_reasons": [
    {{reasonsJson}}
  ],
  "priority_order": [{{orderJson}}],
  "data_references": ["skill_example.md", "ds_example.md"],
  "source_files": ["skill_example.md"],
  "kpi_references": [
    { "physical_name": "example_metric", "value": "0.0", "unit": "%", "table": "example_ai.risk_summary" }
  ]
}
""";
    }
}
