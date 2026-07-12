# 30. Foundry Operate ダッシュボード v1 API 連携 調査記録

関連: [開発計画 README](README.md) | [13-foundry-runtime-config.md](13-foundry-runtime-config.md) | [18-observability-check.md](18-observability-check.md) | [23-foundry-fix.md](23-foundry-fix.md)

---

## 背景

Hosted Agent 側で OpenTelemetry GenAI セマンティクス Activity を `ActivitySource "Nexus6.NewsAnalysisAgent.GenAI"` から発行し、`gen_ai.agent.id` / `gen_ai.agent.name` を Foundry 登録 ID (`web-ag:1` / `impact-ag:2` / `sub-mobile-ag:2` / `sub-internet-ag:2` / `sub-fintech-ag:2`) に差し替えた結果、Application Insights 側のテーブル (`AppDependencies` / `AppGenAIContent` / `AppTraces`) では 5 エージェント全分のトレース + トークン使用量が反映されるようになった。

一方 **Microsoft Foundry ポータルの「Operate」ダッシュボード** には `web-ag:1` のみが表示され、残り 4 エージェントは反映されない事象が継続。原因切り分けと対応方針の調査を実施した。

| 項目 | 値 |
|---|---|
| 調査日 | 2026-06-28 |
| 対象 ACA Revision | `ca-nexus6-hosted-agent--0000031` |
| App Insights | `appi-nexus6-swc` (workspace-based, SamplingPercentage=100) |
| Foundry Project | `proj-PartnerIQ` (`fd-PartnerIQ`) |
| 判定 | **保留 / Microsoft 正式 SDK・API 公開待ち** |

## 観測ファクト

### Foundry Operate 表示要件

- ダッシュボードに表示される「会話」「応答」は **`conv_*` / `resp_*` ID 体系** で識別される。
- 既存 Hosted Agent は Foundry **Assistants API** (`api-version=2025-05-01`) 経由でエージェントを実行しており、生成 ID は **`thread_*` / `run_*` / `asst_*`**。
- Assistants API 由来のトレースは Operate ダッシュボードに反映されない（Application Insights には反映済み）。

### v1 Agent Service API 直接呼び出し検証

`https://fd-partneriq.services.ai.azure.com/api/projects/proj-PartnerIQ` を基点に複数経路を検証。

| 試行 | 結果 |
|---|---|
| `GET /agents/web-ag?api-version=v1` | **200**（`protocols:["responses"]`, `kind:"prompt"`, `definition.model="gpt-5.4"`） |
| `POST /conversations?api-version=v1` | **200**（`conv_*` ID 返却） |
| `POST /conversations/{conv_id}/responses?api-version=v1` | **400** `"API operation not supported for token authentication: ApiId azure-ai-projects OperationId Conversations_Wildcard_Post not supported for CheckAccess"` |
| `POST /agents/{id}/openai/v1/responses?api-version=v1` | **404** |
| `POST /agents/{id}/openai/v1/responses?api-version=<dated各種>` | **400** `"API version not supported"` |
| `POST /openai/v1/responses` (`model=web-ag`, `cognitiveservices.azure.com` スコープ) | **404** `DeploymentNotFound` |

### Foundry Playground 内部通信

Playwright で Playground のチャット送信時のネットワーク通信を取得した結果、**公開 Azure API ではなく `ai.azure.com/nextgen/*` という Microsoft ポータル独自プロキシ経由**であることが判明。

| エンドポイント | 用途 |
|---|---|
| `POST https://ai.azure.com/nextgen/api/query?createAgentConversationResolver` | 会話作成 (resolver, body に `useFoundryV2:true`) |
| `POST https://ai.azure.com/nextgen/api/agentchatcompletions` | メッセージ送信 (body: `{"completionsPayload":{"assistant_id":"web-ag","version":"1","inputText":"...","stream":true},"resourceId":"...","threadId":"conv_..."}`) |

- `conv_*` / `resp_*` ID はこのプロキシ経由でのみ生成される模様。
- ポータル UI は Entra ユーザートークン + 内部権限スコープで動作しており、ACA Managed Identity (Azure AI Project Manager 等) の権限スコープでは `/conversations/{id}/responses` を叩けない。

### SDK 候補確認

NuGet 公式パッケージのバージョン棚卸し結果:

| パッケージ | 最新安定 | 最新プレビュー |
|---|---|---|
| `Azure.AI.Agents.Persistent` | `1.1.0` | `1.2.0-beta.10` |
| `Azure.AI.Projects` | `1.1.0` | `1.2.0-beta.5` |

`Azure.AI.Agents.Persistent` は名称の通り **Assistants API (`thread_*` / `run_*` / `asst_*`) のラッパー**であり、`conv_*` / `resp_*` を生成する v1 Responses API ではない。SDK 経由でも Operate ダッシュボード対応の ID 体系は得られない。

## 結論

- **v1 Agent Service の `responses` 呼び出しは現時点で顧客側 SDK / 公開 API では実現不可**。Microsoft ポータル内部プロキシのみが利用できる構造。
- 公開ドキュメント・SDK の対応待ちとして本トラックを保留する。
- 既存の OpenTelemetry GenAI Activity 連携で Application Insights 側のオブザーバビリティ要件は満たされているため、運用上のブロッカーではない。

### 保留時点で確保されている可観測性

| 観測点 | 対応テーブル | 反映状況 |
|---|---|---|
| 5 エージェントのチャットスパン | `AppDependencies` (`gen_ai.usage.*` プロパティ含む) | 反映済み |
| メッセージ本文 | `AppGenAIContent` (`InputMessages` / `OutputMessages`) | 反映済み |
| 構造化ログ | `AppTraces` (FoundryAgentId / input_tokens / output_tokens) | 反映済み |
| Foundry Operate ダッシュボード | Microsoft ポータル | `web-ag:1` のみ反映（保留） |

## 復帰トリガー

以下の条件が満たされた時点で再開を検討する。

1. Microsoft Learn / Azure docs に Agent Service v1 `responses` の顧客向け API 仕様が公開される。
2. `Azure.AI.Agents.Persistent` または別 SDK が `Conversations.CreateResponseAsync` 相当を Token 認証で公開する。
3. Foundry ポータルが Assistants API 由来トレースの取り込みをサポートする。

## 参照

- [13-foundry-runtime-config.md](13-foundry-runtime-config.md) — Foundry エージェント定義・Trigger Agent 構成
- [18-observability-check.md](18-observability-check.md) — App Insights テレメトリ反映確認
- [23-foundry-fix.md](23-foundry-fix.md) — Foundry gpt-5.4 経路修正
- Memory: `/memories/repo/observability.md` — 本調査由来の運用知見
