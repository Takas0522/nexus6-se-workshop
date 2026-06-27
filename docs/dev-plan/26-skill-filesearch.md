# 26. Skill/DS File Search 統合

関連: [Skill.md / DS.md 設計](08-skill-ds-design.md) | [ACA デプロイ](15-aca-deployment.md)

---

## 設計

Agent 2（BusinessImpact）/ Agent 3（DivisionRecommend）は `FoundryAssistantsClient` 経由で Foundry Assistants API を呼び出す。

| 項目 | 値 |
|---|---|
| Project endpoint | `https://fd-partneriq.services.ai.azure.com/api/projects/proj-PartnerIQ` |
| API version | `2025-05-01` |
| 認証 scope | `https://ai.azure.com/.default`（Project-scoped Agents API 用） |
| Model | `gpt-5.4` |
| Vector store | `vs_EN0WyOWKa7aVn0STee8oFhZ7` |
| Impact Assistant | `asst_RwluAV63cXNzwFEY8427qa1g` |
| Recommend Assistant | `asst_JlAqnycokGHDPGpYUO7pvxSS` |

> ルート `/openai/assistants` は `2025-04-01-preview` で応答するが、この Vector Store は Project-scoped `/api/projects/...` 側に存在するため、実装は Project-scoped `/assistants` / `/threads` を採用した。

## 呼び出しシーケンス

1. `GET /assistants?limit=100&order=desc` で既存 Assistant 名を検索。
2. なければ `POST /assistants` で `tools: [{ "type": "file_search" }]` と `tool_resources.file_search.vector_store_ids` を設定。
3. `POST /threads`、`POST /threads/{threadId}/messages` でユーザー入力を投入。
4. `POST /threads/{threadId}/runs` で Assistant Run を開始。
5. 最大 30 秒 poll し、`completed` 後に `GET /threads/{threadId}/messages` から assistant 出力を取得。
6. `GET /threads/{threadId}/runs/{runId}/steps` で `file_search` tool call をログ確認。
7. message annotation の file id は `/files/{fileId}` でファイル名へ解決し、JSON の `data_references` に追記する。

## Env vars

```bash
Foundry__AssistantsApiVersion=2025-05-01
Foundry__Assistant__RunMaxWaitSeconds=30
Foundry__Assistant__ImpactAssistantId=asst_RwluAV63cXNzwFEY8427qa1g
Foundry__Assistant__RecommendAssistantId=asst_JlAqnycokGHDPGpYUO7pvxSS
Foundry__FileSearchVectorStoreId=vs_EN0WyOWKa7aVn0STee8oFhZ7
```

## 検証ログ

| 項目 | 結果 |
|---|---|
| `dotnet test src/news-analysis-agent/NewsAnalysisAgent.sln --no-restore` | 24/24 passed |
| Docker image | `crnexus6swc.azurecr.io/nexus6-hosted-agent:w-skill-ds-filesearch-20260627-5` |
| ACA revision | `ca-nexus6-hosted-agent--0000015` Running / Healthy |
| ACA file_search log | `AssistantId=asst_JlAqnycokGHDPGpYUO7pvxSS; file_search_used=True` |

### E2E

| シナリオ | Skill/DS 引用確認 |
|---|---|
| S1 日銀利上げ | `fintech_skill_mortgage-rate-hike.md`, `mobile_skill_boj-installment.md`, `ds_fintech_ai.md` を引用。住宅ローン/リボ/延滞率/NIM の閾値が出力された。 |
| S2 5G SA / MNP | `mobile_skill_competitor-mnp.md`, `mobile_skill_fx-impact.md`, `ds_mobile_ai.md` を引用。MNP転出率、更新月、端末補助、海外調達コストが出力された。 |
| S3 生成AI EC | `ecommerce_skill_point-competition.md`, `ds_ecommerce_ai.md` を引用。ポイント差、カート離脱率、Gold/Platinum 流出、5%/10% 変化率閾値が出力された。 |

## 残課題

- file citation annotation にファイル名が返らない Run があるため、プロンプト内の `[source: filename]` と JSON `data_references` を主な証跡にしている。
- Project-scoped Agents API は `https://ai.azure.com/.default` scope が必要。ルート OpenAI API へ戻す場合は Vector Store の所在も合わせる必要がある。
