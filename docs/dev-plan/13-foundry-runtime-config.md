# 13. Foundry ランタイム構成記録

関連: [開発計画 README](README.md) | [Azure 詳細設定](05-azure-configuration.md) | [Skill/DS 設計](08-skill-ds-design.md) | [Azure / M365 構成](09-azure-m365-configuration.md)

---

## 対象環境

| 項目 | 値 |
|---|---|
| Resource Group | `SEWorkShopC12` |
| Foundry リソース | `fd-PartnerIQ` |
| Foundry Project | `proj-PartnerIQ` |
| Project Endpoint | `https://fd-partneriq.services.ai.azure.com/api/projects/proj-PartnerIQ` |
| ADLS Gen2 | `stnexus6skill1t2i` |
| Skill/DS コンテナ | `skill-docs` |
| AI Search バックエンド | `iq-knowledge-source` |
| AI Search 接続 | `iqknowledgesource22i5c0` |
| News Portal | `https://stnexus6portal1t2i.z1.web.core.windows.net/` |
| Storage Queue | `stnexus6skill1t2i` / `news-analysis-jobs` |

---

## モデルデプロイ確認

`az cognitiveservices account deployment list -g SEWorkShopC12 -n fd-PartnerIQ` で以下を確認済み。

| デプロイ名 | モデル | SKU | 容量 | Version |
|---|---|---|---|---|
| `gpt-5.4` | `gpt-5.4` | `GlobalStandard` | 500 | `2026-03-05` |
| `text-embedding-3-large` | `text-embedding-3-large` | `Standard` | 120 | `1` |

---

## Foundry File Search Vector Store

| 項目 | 値 |
|---|---|
| Vector Store 名 | `vs_nexus6_skilldocs` |
| Vector Store ID | `vs_EN0WyOWKa7aVn0STee8oFhZ7` |
| 作成 API | `POST {ProjectEndpoint}/vector_stores?api-version=2025-05-01` |
| ステータス | `completed` |
| 登録ファイル数 | 13 |
| 使用量 | 107,457 bytes |

### 自動化結果

- Vector Store 本体は REST API で作成済み。
- `stnexus6skill1t2i/skill-docs` の Skill/DS Markdown 13 ファイルを ADLS から取得し、Foundry Files API にアップロード後、Vector Store に登録済み。
- API の `data_source` 直結は次のエラーで自動化不可だったため、継続的な ADLS データソース連携は Foundry Portal で手動設定する。

```text
POST /vector_stores/{id}/files?api-version=2025-05-01
{"data_source":{"type":"uri_asset","uri":"https://stnexus6skill1t2i.blob.core.windows.net/skill-docs/..."}}
→ 400 Invalid request. Parameter `data_source` is not supported.
```

### 登録済みファイル

- `ds-docs/ds_ecommerce_ai.md`
- `ds-docs/ds_fintech_ai.md`
- `ds-docs/ds_kpi_monthly_revenue.md`
- `ds-docs/ds_mobile_ai.md`
- `ecommerce/ecommerce_skill_consumer-sentiment.md`
- `ecommerce/ecommerce_skill_fx-crossborder.md`
- `ecommerce/ecommerce_skill_point-competition.md`
- `fintech/fintech_skill_card-share.md`
- `fintech/fintech_skill_fx-exposure.md`
- `fintech/fintech_skill_mortgage-rate-hike.md`
- `mobile/mobile_skill_boj-installment.md`
- `mobile/mobile_skill_competitor-mnp.md`
- `mobile/mobile_skill_fx-impact.md`

### ADLS データソース連携の手動引継ぎ

1. Foundry Portal で `fd-PartnerIQ` / `proj-PartnerIQ` を開く。
2. **Knowledge** → **File Search** → `vs_nexus6_skilldocs` を開く。
3. **Add data source** で Azure Blob / ADLS Gen2 を選択する。
4. Storage account `stnexus6skill1t2i`、container `skill-docs`、path `skill-docs/**` を指定する。
5. バックエンドとして既存 Azure AI Search `iq-knowledge-source`（接続 `iqknowledgesource22i5c0`）を選択する。
6. Embedding deployment は `text-embedding-3-large` を指定する。
7. 接続後、再インデクシングが `completed` になることを確認する。

---

## Grounding with Bing Search 接続

### 確認結果

`az cognitiveservices account connection list -g SEWorkShopC12 -n fd-PartnerIQ` で確認した範囲では、Bing Grounding 接続は未作成。既存接続は以下のみ。

| 接続名 | 種別 |
|---|---|
| `iqknowledgesource22i5c0` | Azure AI Search |
| `fabric_dataagent` | Fabric Data Agent preview |

`appsettings.json` には手動作成待ちの placeholder として `TODO_CREATE_BING_GROUNDING_CONNECTION_ID` を設定する。

### 手動作成手順

1. Azure Portal で Grounding with Bing Search 対応リソースを作成する。
2. Foundry Portal で `proj-PartnerIQ` → **Management center** → **Connections** を開く。
3. **+ New connection** → **Grounding with Bing Search** を選択する。
4. 作成した Bing Grounding リソースを指定し、Project に接続を追加する。
5. 作成後の Connection ID を控え、`Foundry:GroundingBingConnectionId` に反映する。
6. Agent 1 / Trigger Agent の Tool として Grounding with Bing Search を有効化する。

---

## Foundry Scheduled Trigger Agent

### 自動化結果

REST API で `/schedules?api-version=2025-10-15-preview` は参照可能だったが、Tools（WebIQ / Bing Grounding / Azure Storage Queue 書き込み）と Scheduled Trigger を完全構成する安定 API が確認できなかったため、Trigger Agent は手動構成に引き継ぐ。

| 項目 | 値 |
|---|---|
| Trigger Agent ID | `TODO_CREATE_NEWS_PORTAL_POLLER_AGENT_ID` |
| Trigger 名 | `news-portal-poller-hourly` |
| cron | `0 * * * *` |
| Queue | `https://stnexus6skill1t2i.queue.core.windows.net/news-analysis-jobs` |

### Trigger Agent プロンプト

```text
あなたは Nexus6 Demo の News Portal 巡回エージェントです。
毎回 https://stnexus6portal1t2i.z1.web.core.windows.net/ を取得し、記事リンクを列挙してください。
各記事 URL を取得し、本文、URL、公開日時を抽出してください。
未処理の記事だけを Azure Storage Queue news-analysis-jobs に次の JSON で enqueue してください。

{
  "originalNewsText": "<記事本文>",
  "sourceUrl": "<記事URL>",
  "publishedAt": "<ISO8601>"
}

同じ sourceUrl を重複 enqueue しないでください。
記事本文が取得できない場合は enqueue せず、実行ログに理由を残してください。
```

### 手動構成手順

1. Foundry Portal で `proj-PartnerIQ` → **Agents** → **New agent** を開く。
2. Agent 名を `news-portal-poller`、Model を `gpt-5.4` にする。
3. Tool として WebIQ または Grounding with Bing Search を追加し、News Portal URL の fetch を許可する。
4. Azure Storage Queue 書き込み Function Tool / Logic App / Custom Tool を追加する。
5. Foundry 側 Managed Identity に `stnexus6skill1t2i` の `Storage Queue Data Message Sender` を付与する。
6. 上記プロンプトを Instructions に設定する。
7. Scheduled Trigger を作成し、cron `0 * * * *` を設定する。
8. 手動 Run で `news-analysis-jobs` に JSON メッセージが入ることを確認する。
9. 作成された Agent ID / Trigger ID をこの文書と `appsettings.json`（必要な場合）に反映する。
