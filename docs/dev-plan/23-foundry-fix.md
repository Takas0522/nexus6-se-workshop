# 23. Foundry BadRequest 修正

関連: [22. Fabric RBAC](22-fabric-rbac.md), [未解決 TODO](19-open-items.md)

## 症状

- `/devui/run` の `dataReferences` に `mock-skill-ds` が残存。
- ACA console log に `Foundry chat completions returned BadRequest. Falling back to mock client.` が出力。
- Mock fallback により recommendation headline が `mock recommendation based on KPI and Skill/DS.` になっていた。

## 調査結果

ACA 設定:

- FQDN: `ca-nexus6-hosted-agent.ambitiousgrass-408dc79b.swedencentral.azurecontainerapps.io`
- Revision before: `ca-nexus6-hosted-agent--0000003`
- Revision after: `ca-nexus6-hosted-agent--0000004`
- Image after: `crnexus6swc.azurecr.io/nexus6-hosted-agent:t-foundry-badrequest`
- `Foundry__ProjectEndpoint`: `https://fd-partneriq.services.ai.azure.com/api/projects/proj-PartnerIQ`
- `Foundry__DefaultModelDeployment`: `gpt-5.4`
- `Foundry__ApiVersion`: `2024-10-21`
- `Foundry__MaxCompletionTokens`: `4096`

Foundry deployment:

- `fd-PartnerIQ` deployment `gpt-5.4` is Running.
- Model: `gpt-5.4`, version `2026-03-05`.
- `text-embedding-3-large` deployment is also Running.

RBAC:

- ACA managed identity `65c3be76-e682-43d7-b8d4-7a31b9acd1f6` has `Cognitive Services User` on `fd-PartnerIQ`.

## 原因

`FoundryAgentClient` が Chat Completions payload に `max_tokens` を送っていた。
`gpt-5.4` は `max_tokens` を受け付けず、`max_completion_tokens` が必要。

手動再現:

```json
{
  "error": {
    "message": "Unsupported parameter: 'max_tokens' is not supported with this model. Use 'max_completion_tokens' instead.",
    "type": "invalid_request_error",
    "param": "max_tokens",
    "code": "unsupported_parameter"
  }
}
```

## 修正内容

- `FoundryAgentClient` の payload を `max_tokens` から `max_completion_tokens` に変更。
- `Foundry:ApiVersion` と `Foundry:MaxCompletionTokens` を設定可能にした。
- BadRequest 再発時に status / deployment / api-version / error body を ACA log に出すようにした。
- Mock fallback は保険として維持。

## 検証

`dotnet test src/news-analysis-agent/NewsAnalysisAgent.sln --no-restore --verbosity minimal`

- Unit tests: 20 passed
- Integration tests: 1 passed

E2E:

```bash
curl -X POST https://ca-nexus6-hosted-agent.ambitiousgrass-408dc79b.swedencentral.azurecontainerapps.io/devui/run \
  -H 'Content-Type: application/json' \
  -d '{"originalNewsText":"日銀が政策金利を0.5%に引き上げ。住宅ローン金利上昇懸念。"}'
```

After:

- `dataReferences` に `mock-skill-ds` なし。
- `summary` は日銀利上げ・住宅ローン金利への影響を説明する LLM 生成文。
- `impactScores`: Fintech high / Mobile medium / Ecommerce low。
- ACA log に `Invoking Foundry chat completions deployment gpt-5.4 api-version 2024-10-21.` が出力され、BadRequest は再発していない。

## 残課題

- Teams Workflows URL は未設定のため通知は mock fallback のまま。
- Bing Grounding 接続 ID は未設定のため、必要に応じて別トラックで設定する。
