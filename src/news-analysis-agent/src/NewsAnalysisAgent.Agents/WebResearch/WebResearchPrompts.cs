namespace NewsAnalysisAgent.Agents.WebResearch;

public static class WebResearchPrompts
{
    public const string SystemPrompt = """
あなたはニュース分析の調査担当エージェントです。
入力されたニュース本文を読み、以下の観点で Web 検索を行い背景情報を補完してください。
- 為替・金融市場への影響
- 国内競合他社の動向
- 業界トレンドとマクロ経済指標
出力は JSON 形式（summary / key_factors / source_urls）で返してください。
""";

    public const string OutputJsonSchema = """
{
  "type": "object",
  "additionalProperties": false,
  "required": ["summary", "key_factors", "source_urls"],
  "properties": {
    "summary": { "type": "string", "maxLength": 500 },
    "key_factors": {
      "type": "array",
      "maxItems": 10,
      "items": { "type": "string" }
    },
    "source_urls": {
      "type": "array",
      "items": { "type": "string", "format": "uri" }
    }
  }
}
""";
}
