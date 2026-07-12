# DS: Hosted Agent データマッピング定義

## 前提

- 本文書は nexus6-se-workshop の Demo 用 DS.md である。
- 数値例、テーブル名、業務事例は架空であり、実在企業の機密を含まない。
- 各 Hosted Agent がどのデータソースを参照・書き込みするかを定義する。
- SQL の方言は Fabric SQL Analytics Endpoint を想定する。
- スキル定義の詳細は `knowledge/skill/` 配下の各スキル MD を参照すること。
- テーブル構造・リレーションの詳細は `knowledge/ds/fabric-ontology.ds.md` を参照すること。

---

## エージェント構成概要

```
┌─────────────┐    ニュース     ┌─────────────┐   KPIデータ    ┌─────────────┐
│  Agent 1    │──────────────→│  Agent 2    │──────────────→│  Agent 3    │
│ Web情報収集  │   構造化記事    │ インパクト評価 │  影響判定結果   │ 事業部別     │
│(Bing Ground)│               │(Fabric KPI) │               │ レコメンド    │
└─────────────┘               └─────────────┘               └──────┬──────┘
                                                                    │
                                                              レコメンド結果
                                                                    │
                                                                    ↓
                                                             ┌─────────────┐
                                                             │  Agent 4    │
                                                             │  通知送信    │
                                                             │  (Teams)    │
                                                             └─────────────┘
```

| Agent | 役割 | 入力 | 出力 | データアクセス |
|---|---|---|---|---|
| Agent 1 | Web情報収集 | ニューストリガー（RSS/手動） | 構造化ニュース記事 | Bing Grounding（読取専用） |
| Agent 2 | ビジネスインパクト評価 | 構造化ニュース記事 | 影響判定結果（スコア・レベル） | Fabric KPI テーブル（読取専用） |
| Agent 3 | 事業部別レコメンド | 影響判定結果 | 推奨アクション・通知内容 | 業務DB（読取専用） |
| Agent 4 | 通知送信 | 推奨アクション・通知内容 | Teams メッセージ | Teams API（書込） |

---

## Agent 1: Web情報収集（Bing Grounding）

### データソース

| ソース | アクセス種別 | 説明 |
|---|---|---|
| Bing Grounding API | 読取 | ニュース検索・記事取得 |
| （DB アクセスなし） | — | Agent 1 は Fabric テーブルを参照しない |

### 出力スキーマ

Agent 1 が Agent 2 に渡す構造化ニュース記事のスキーマ:

```json
{
  "news_id": "string",
  "title": "string",
  "summary": "string (500文字以内)",
  "source_url": "string",
  "published_at": "datetime",
  "news_category": "string (supply_chain | data_privacy_regulation | cyber_security | ...)",
  "extracted_parameters": {
    "// ニュースカテゴリに応じたパラメータ群": "// 各スキルの入力パラメータ「ニュース情報」に対応"
  },
  "affected_domains_hint": ["MOBILE", "SNS", "SI"]
}
```

### カテゴリ判定ルール

| ニュースカテゴリ | 判定キーワード（例） | 対応スキル接頭辞 |
|---|---|---|
| `supply_chain` | 半導体, サプライチェーン, 輸出規制, 調達遅延, チップ不足 | `*_skill_semiconductor-shortage` |
| `data_privacy_regulation` | 個人情報保護法, プライバシー, GDPR, 同意, オプトイン, 制裁金 | `*_skill_data-privacy-regulation` |
| `cyber_security` | サイバー攻撃, DDoS, ランサムウェア, クラウド障害, データ漏洩 | `*_skill_cloud-cyber-attack` |

---

## Agent 2: ビジネスインパクト評価（Fabric KPI データ参照）

### データソース

Agent 2 は Gold 層の KPI テーブルおよび Silver 層の集計データを参照し、ニュースカテゴリに応じたスキルの判断ロジックを実行する。

#### 常時参照テーブル（全スキル共通）

| テーブル | DB | 用途 | 主要クエリ |
|---|---|---|---|
| `unified_customers` | sqldb_common_01 | 総顧客数の取得 | `SELECT COUNT(*) FROM unified_customers` |
| `domain_id_mappings` | sqldb_common_01 | 領域別顧客数の取得 | `SELECT domain_code, COUNT(*) ... GROUP BY domain_code` |
| `customer_segments` | sqldb_common_01 | PREMIUMセグメント比率・リスクスコア | `SELECT segment_code, COUNT(*), AVG(risk_score) ... GROUP BY segment_code` |

#### ニュースカテゴリ別の参照テーブル

##### supply_chain（半導体供給制約）

| テーブル | DB | 取得データ | クエリパターン |
|---|---|---|---|
| `mobile_device_inventory` | sqldb_mobile_01 | 在庫水準・メーカー別在庫 | Q-SC-M1 |
| `mobile_contracts` | sqldb_mobile_01 | アクティブ契約数・プラン別分布 | Q-SC-M2 |
| `sns_transactions` | sqldb_sns_01 | 月間広告収益 | Q-SC-S1 |
| `sns_ad_campaigns` | sqldb_sns_01 | 稼働中キャンペーン数 | Q-SC-S2 |
| `si_projects` | sqldb_si_01 | 進行中PJ数・HW調達PJ数 | Q-SC-I1 |
| `si_contracts` | sqldb_si_01 | HW調達予算・ペナルティ条項PJ数 | Q-SC-I2 |
| `si_engineers` | sqldb_si_01 | アサインエンジニア数・時間単価 | Q-SC-I3 |

##### data_privacy_regulation（個人情報保護法）

| テーブル | DB | 取得データ | クエリパターン |
|---|---|---|---|
| `mobile_customers` | sqldb_mobile_01 | アクティブ顧客数 | Q-DP-M1 |
| `mobile_contracts` | sqldb_mobile_01 | 契約数・マーケティング施策対象 | Q-DP-M2 |
| `mobile_transactions` | sqldb_mobile_01 | 月間売上（制裁金基準） | Q-DP-M3 |
| `sns_users` | sqldb_sns_01 | アクティブユーザー数 | Q-DP-S1 |
| `sns_transactions` | sqldb_sns_01 | 広告収益・収益分類 | Q-DP-S2 |
| `sns_ad_campaigns` | sqldb_sns_01 | 広告主数・キャンペーン数 | Q-DP-S3 |
| `sns_subscriptions` | sqldb_sns_01 | サブスク収益（代替収益源） | Q-DP-S4 |
| `si_customers` | sqldb_si_01 | 業種別顧客数 | Q-DP-I1 |
| `si_projects` | sqldb_si_01 | 個人データ取扱PJ数 | Q-DP-I2 |
| `si_engineers` | sqldb_si_01 | プライバシー資格保有者数 | Q-DP-I3 |

##### cyber_security（サイバー攻撃）

| テーブル | DB | 取得データ | クエリパターン |
|---|---|---|---|
| `mobile_customers` | sqldb_mobile_01 | アクティブ顧客数 | Q-CS-M1 |
| `mobile_contracts` | sqldb_mobile_01 | 日次オンライン契約件数 | Q-CS-M2 |
| `sns_users` | sqldb_sns_01 | DAU・同時接続数 | Q-CS-S1 |
| `sns_transactions` | sqldb_sns_01 | 日次広告収益 | Q-CS-S2 |
| `sns_ad_campaigns` | sqldb_sns_01 | 稼働中キャンペーン数（SLA補填対象） | Q-CS-S3 |
| `sns_subscriptions` | sqldb_sns_01 | アクティブサブスク数（解約リスク） | Q-CS-S4 |
| `si_projects` | sqldb_si_01 | 運用保守PJ数 | Q-CS-I1 |
| `si_contracts` | sqldb_si_01 | SLA契約数・ペナルティ単価 | Q-CS-I2 |
| `si_engineers` | sqldb_si_01 | 即時対応可能エンジニア数 | Q-CS-I3 |
| `si_customers` | sqldb_si_01 | 影響顧客数 | Q-CS-I4 |

### 取得クエリパターン

#### Q-SC-M1: 在庫水準（半導体供給制約 × 携帯電話）

```sql
SELECT
  manufacturer,
  device_model,
  SUM(stock_quantity) AS total_stock,
  AVG(unit_cost) AS avg_unit_cost,
  COUNT(DISTINCT warehouse_code) AS warehouse_count
FROM sqldb_mobile_01.mobile_device_inventory
WHERE status = 'IN_STOCK'
GROUP BY manufacturer, device_model
ORDER BY total_stock ASC;
```

#### Q-SC-I1: 進行中PJ（半導体供給制約 × SI）

```sql
SELECT
  project_type,
  COUNT(*) AS project_count,
  SUM(contract_amount) AS total_amount,
  SUM(assigned_engineer_count) AS total_engineers,
  MIN(end_date) AS earliest_deadline
FROM sqldb_si_01.si_projects
WHERE status = 'IN_PROGRESS'
GROUP BY project_type;
```

#### Q-DP-S2: 広告収益分類（個人情報保護法 × SNS）

```sql
SELECT
  transaction_type,
  COUNT(*) AS tx_count,
  SUM(amount) AS total_amount,
  SUM(amount) * 1.0 / SUM(SUM(amount)) OVER () AS revenue_ratio
FROM sqldb_sns_01.sns_transactions
WHERE transaction_date >= DATEADD(MONTH, -1, GETDATE())
GROUP BY transaction_type;
```

#### Q-DP-I3: プライバシー資格保有者数（個人情報保護法 × SI）

```sql
SELECT
  COUNT(*) AS privacy_engineers
FROM sqldb_si_01.si_engineers
WHERE skill_set LIKE '%privacy%'
   OR skill_set LIKE '%security%'
   OR skill_set LIKE '%ISMS%'
   OR skill_set LIKE '%CIPP%';
```

#### Q-CS-S1: DAU推定（サイバー攻撃 × SNS）

```sql
SELECT
  COUNT(DISTINCT sns_user_id) AS estimated_dau
FROM sqldb_sns_01.sns_users
WHERE status = 'ACTIVE'
  AND last_login_at >= DATEADD(DAY, -1, GETDATE());
```

#### Q-CS-I3: 即時対応可能エンジニア（サイバー攻撃 × SI）

```sql
SELECT
  availability_status,
  COUNT(*) AS engineer_count,
  AVG(hourly_rate) AS avg_hourly_rate
FROM sqldb_si_01.si_engineers
WHERE availability_status IN ('AVAILABLE', 'ON_CALL')
GROUP BY availability_status;
```

#### Q-COMMON-SEG: PREMIUMセグメント分析（全スキル共通）

```sql
SELECT
  dm.domain_code,
  cs.segment_code,
  COUNT(*) AS customer_count,
  AVG(cs.risk_score) AS avg_risk_score,
  AVG(cs.lifetime_value) AS avg_ltv
FROM sqldb_common_01.customer_segments cs
JOIN sqldb_common_01.domain_id_mappings dm
  ON cs.unified_customer_id = dm.unified_customer_id
WHERE dm.is_active = 1
GROUP BY dm.domain_code, cs.segment_code;
```

### 出力スキーマ

Agent 2 が Agent 3 に渡す影響判定結果:

```json
{
  "news_id": "string",
  "news_category": "string",
  "evaluation_timestamp": "datetime",
  "domain_impacts": [
    {
      "domain_code": "MOBILE | SNS | SI",
      "skill_id": "string (例: mobile_skill_semiconductor-shortage)",
      "impact_score": 0-100,
      "impact_level": "critical | high | medium | low",
      "key_metrics": {
        "// スキルごとに異なる主要指標": "値"
      },
      "data_snapshot": {
        "// 判断に使用したデータのスナップショット": "値"
      }
    }
  ],
  "cross_domain_summary": {
    "max_impact_level": "string",
    "affected_domain_count": 0-3,
    "total_financial_risk_jpy": 0
  }
}
```

---

## Agent 3: 事業部別レコメンド（各領域の業務データ参照）

### データソース

Agent 3 は Agent 2 の判定結果を受けて、影響を受ける業務領域のデータを詳細参照し、具体的な推奨アクション・通知内容を生成する。

#### 領域別の参照テーブルと用途

##### 携帯電話事業のレコメンド生成時

| テーブル | DB | 用途 | 詳細 |
|---|---|---|---|
| `mobile_customers` | sqldb_mobile_01 | 影響顧客の特定 | status=ACTIVE の顧客数、customer_type 別分布 |
| `mobile_contracts` | sqldb_mobile_01 | 契約状況の確認 | プラン別契約数、更新期限が近い契約の特定 |
| `mobile_transactions` | sqldb_mobile_01 | 直近の取引動向 | 月次取引額推移、取引種別の分布 |
| `mobile_device_inventory` | sqldb_mobile_01 | 在庫の詳細確認 | モデル別・倉庫別の在庫詳細 |
| `customer_segments` | sqldb_common_01 | 優先顧客の特定 | PREMIUM セグメントの携帯電話顧客リスト |

##### SNS事業のレコメンド生成時

| テーブル | DB | 用途 | 詳細 |
|---|---|---|---|
| `sns_users` | sqldb_sns_01 | ユーザー基盤の確認 | アカウント種別別分布、アクティブ率 |
| `sns_subscriptions` | sqldb_sns_01 | サブスク転換余地 | プラン別契約数、自動更新率 |
| `sns_transactions` | sqldb_sns_01 | 収益構造の詳細 | 収益種別の月次推移、広告単価の傾向 |
| `sns_ad_campaigns` | sqldb_sns_01 | 広告主への影響 | 広告主別の予算・消化状況、SLA対象の特定 |
| `customer_segments` | sqldb_common_01 | 優先ユーザーの特定 | PREMIUM セグメントの SNS ユーザーリスト |

##### SI事業のレコメンド生成時

| テーブル | DB | 用途 | 詳細 |
|---|---|---|---|
| `si_customers` | sqldb_si_01 | 影響顧客の特定 | 業種別分布、影響業種の顧客リスト |
| `si_projects` | sqldb_si_01 | PJポートフォリオ確認 | PJ種別・ステータス・工期の詳細 |
| `si_contracts` | sqldb_si_01 | 契約条件の確認 | 契約種別、ペナルティ条項、SLA契約の特定 |
| `si_transactions` | sqldb_si_01 | 取引実績の確認 | HW調達額、請求・入金の推移 |
| `si_engineers` | sqldb_si_01 | リソース配置の確認 | スキル別・グレード別の稼働状況 |
| `customer_segments` | sqldb_common_01 | 優先顧客の特定 | PREMIUM セグメントの SI 顧客リスト |

### レコメンド生成クエリパターン

#### Q-REC-M-INV: 在庫不足モデルの特定（半導体供給制約 × 携帯電話）

```sql
SELECT
  di.device_model,
  di.manufacturer,
  SUM(di.stock_quantity) AS current_stock,
  COALESCE(sales.monthly_sales, 0) AS monthly_sales,
  CASE
    WHEN COALESCE(sales.monthly_sales, 0) = 0 THEN 999
    ELSE SUM(di.stock_quantity) * 1.0 / sales.monthly_sales
  END AS months_of_stock
FROM sqldb_mobile_01.mobile_device_inventory di
LEFT JOIN (
  SELECT description AS device_model, COUNT(*) AS monthly_sales
  FROM sqldb_mobile_01.mobile_transactions
  WHERE transaction_type = 'DEVICE_PURCHASE'
    AND transaction_date >= DATEADD(MONTH, -1, GETDATE())
  GROUP BY description
) sales ON di.device_model = sales.device_model
WHERE di.status = 'IN_STOCK'
GROUP BY di.device_model, di.manufacturer, sales.monthly_sales
HAVING SUM(di.stock_quantity) * 1.0 / NULLIF(sales.monthly_sales, 0) < 3
ORDER BY months_of_stock ASC;
```

#### Q-REC-S-ADV: 影響を受ける広告主の特定（個人情報保護法 × SNS）

```sql
SELECT
  ac.advertiser_id,
  COUNT(*) AS active_campaigns,
  SUM(ac.budget) AS total_budget,
  SUM(ac.spent_amount) AS total_spent,
  SUM(ac.budget - ac.spent_amount) AS remaining_budget
FROM sqldb_sns_01.sns_ad_campaigns ac
WHERE ac.status = 'ACTIVE'
  AND ac.end_date >= GETDATE()
GROUP BY ac.advertiser_id
ORDER BY total_budget DESC;
```

#### Q-REC-I-TRIAGE: 顧客システム復旧優先度（サイバー攻撃 × SI）

```sql
SELECT
  sp.project_id,
  sp.project_name,
  sc.company_name,
  sc.si_customer_id,
  sp.project_type,
  sp.contract_amount,
  cs_seg.segment_code,
  cs_seg.lifetime_value,
  CASE
    WHEN cs_seg.segment_code = 'PREMIUM' THEN 1
    WHEN sp.contract_amount >= 50000000 THEN 2
    ELSE 3
  END AS priority_rank
FROM sqldb_si_01.si_projects sp
JOIN sqldb_si_01.si_customers sc ON sp.si_customer_id = sc.si_customer_id
LEFT JOIN sqldb_common_01.domain_id_mappings dm
  ON sc.si_customer_id = dm.domain_customer_id
  AND dm.domain_code = 'SI'
  AND dm.is_active = 1
LEFT JOIN sqldb_common_01.customer_segments cs_seg
  ON dm.unified_customer_id = cs_seg.unified_customer_id
WHERE sp.status = 'MAINTENANCE'
ORDER BY priority_rank ASC, sp.contract_amount DESC;
```

### 出力スキーマ

Agent 3 が Agent 4 に渡す通知内容:

```json
{
  "news_id": "string",
  "evaluation_timestamp": "datetime",
  "notifications": [
    {
      "domain_code": "MOBILE | SNS | SI",
      "impact_level": "critical | high | medium | low",
      "channel": "slack | email | slack + email | slack + email + phone | 定期レポート",
      "recipients": ["チーム名 or 個人"],
      "title": "通知タイトル (例: 【critical】半導体供給制約による端末在庫不足リスク)",
      "body": "通知本文（各スキルの通知テンプレートに基づく）",
      "recommended_actions": ["アクション1", "アクション2"],
      "data_attachments": [
        {
          "name": "在庫不足モデル一覧",
          "query_id": "Q-REC-M-INV",
          "format": "table"
        }
      ]
    }
  ]
}
```

---

## Agent 4: 通知送信（Teams）

### データソース

| ソース | アクセス種別 | 説明 |
|---|---|---|
| Agent 3 出力 | 読取 | 通知内容（JSON） |
| Teams API | 書込 | Adaptive Card メッセージの送信 |
| （DB アクセスなし） | — | Agent 4 は Fabric テーブルを直接参照しない |

### 通知チャネルマッピング

| impact_level | domain_code | Teams チャネル | メンション対象 |
|---|---|---|---|
| critical | MOBILE | `携帯電話事業-緊急` | @事業部長, @該当部門マネージャー |
| critical | SNS | `SNS事業-緊急` | @事業部長, @該当部門マネージャー |
| critical | SI | `SI事業-緊急` | @事業部長, @該当PMO |
| high | MOBILE | `携帯電話事業-アラート` | @該当部門マネージャー |
| high | SNS | `SNS事業-アラート` | @該当部門マネージャー |
| high | SI | `SI事業-アラート` | @該当PMO |
| medium | * | `リスク管理-通常` | @リスク管理チーム |
| low | * | `リスク管理-定期レポート` | （メンションなし、週次ダイジェスト） |

### Adaptive Card テンプレート構造

```json
{
  "type": "AdaptiveCard",
  "version": "1.5",
  "body": [
    { "type": "TextBlock", "text": "{title}", "size": "Large", "weight": "Bolder", "color": "{impact_level_color}" },
    { "type": "FactSet", "facts": [
      { "title": "インパクトスコア", "value": "{impact_score}/100" },
      { "title": "影響レベル", "value": "{impact_level}" },
      { "title": "対象領域", "value": "{domain_code}" }
    ]},
    { "type": "TextBlock", "text": "{body}", "wrap": true },
    { "type": "TextBlock", "text": "推奨アクション", "weight": "Bolder" },
    { "type": "TextBlock", "text": "{recommended_actions}", "wrap": true }
  ],
  "actions": [
    { "type": "Action.OpenUrl", "title": "詳細レポート", "url": "{report_url}" },
    { "type": "Action.OpenUrl", "title": "元記事", "url": "{source_url}" }
  ]
}
```

---

## スキルとデータの対応関係

### マトリクス: スキル × テーブル参照

| テーブル | mobile× 半導体 | mobile× 個情法 | mobile× サイバー | sns× 半導体 | sns× 個情法 | sns× サイバー | si× 半導体 | si× 個情法 | si× サイバー |
|---|---|---|---|---|---|---|---|---|---|
| `unified_customers` | ○ | ○ | ○ | ○ | ○ | ○ | ○ | ○ | ○ |
| `domain_id_mappings` | ○ | ○ | ○ | ○ | ○ | ○ | ○ | ○ | ○ |
| `customer_segments` | ○ | ○ | ○ | ○ | ○ | ○ | ○ | ○ | ○ |
| `mobile_customers` | ○ | ○ | ◎ | | | | | | |
| `mobile_contracts` | ○ | ○ | ◎ | | | | | | |
| `mobile_transactions` | ○ | ◎ | ○ | | | | | | |
| `mobile_device_inventory` | ◎ | | ○ | | | | | | |
| `sns_users` | | | | ○ | ○ | ◎ | | | |
| `sns_subscriptions` | | | | | ○ | ◎ | | | |
| `sns_transactions` | | | | ◎ | ◎ | ◎ | | | |
| `sns_ad_campaigns` | | | | ○ | ◎ | ○ | | | |
| `si_customers` | | | | | | | ○ | ◎ | ◎ |
| `si_projects` | | | | | | | ◎ | ◎ | ◎ |
| `si_contracts` | | | | | | | ◎ | ○ | ◎ |
| `si_transactions` | | | | | | | ○ | ○ | ○ |
| `si_engineers` | | | | | | | ◎ | ◎ | ◎ |

凡例: ◎=主要参照（スキルの判断ロジックに直接使用）, ○=補助参照（セグメント分析・補足情報）

### マトリクス: エージェント × テーブル × アクセス種別

| テーブル | Agent 1 | Agent 2 | Agent 3 | Agent 4 |
|---|---|---|---|---|
| `unified_customers` | — | R (集計) | R (詳細) | — |
| `domain_id_mappings` | — | R (集計) | R (結合) | — |
| `customer_segments` | — | R (集計) | R (優先度) | — |
| `mobile_customers` | — | R (集計) | R (詳細) | — |
| `mobile_contracts` | — | R (集計) | R (詳細) | — |
| `mobile_transactions` | — | R (集計) | R (推移) | — |
| `mobile_device_inventory` | — | R (集計) | R (詳細) | — |
| `sns_users` | — | R (集計) | R (詳細) | — |
| `sns_subscriptions` | — | R (集計) | R (詳細) | — |
| `sns_transactions` | — | R (集計) | R (推移) | — |
| `sns_ad_campaigns` | — | R (集計) | R (詳細) | — |
| `si_customers` | — | R (集計) | R (詳細) | — |
| `si_projects` | — | R (集計) | R (詳細) | — |
| `si_contracts` | — | R (集計) | R (詳細) | — |
| `si_transactions` | — | R (集計) | R (推移) | — |
| `si_engineers` | — | R (集計) | R (詳細) | — |
| Bing Grounding API | R | — | — | — |
| Teams API | — | — | — | W |

凡例: R=読取, W=書込, —=アクセスなし

---

## データフロー詳細

### シーケンス図

```
User/Trigger  Agent1      Agent2           Fabric DB        Agent3         Agent4       Teams
    │           │            │                │               │              │            │
    │──trigger──→            │                │               │              │            │
    │           │──Bing検索──→                │               │              │            │
    │           │←─記事取得──┤                │               │              │            │
    │           │            │                │               │              │            │
    │           │──構造化記事─→               │               │              │            │
    │           │            │──KPI取得───────→               │              │            │
    │           │            │←─集計データ────┤               │              │            │
    │           │            │                │               │              │            │
    │           │            │──スキル実行────→               │              │            │
    │           │            │  (3領域×該当)  │               │              │            │
    │           │            │←─スコア算出────┤               │              │            │
    │           │            │                │               │              │            │
    │           │            │──影響判定結果──────────────────→              │            │
    │           │            │                │               │              │            │
    │           │            │                │               │──業務データ──→            │
    │           │            │                │               │←─詳細データ──┤            │
    │           │            │                │               │              │            │
    │           │            │                │               │──レコメンド生成            │
    │           │            │                │               │              │            │
    │           │            │                │               │──通知内容────→            │
    │           │            │                │               │              │──Card送信──→
    │           │            │                │               │              │←─送信結果──┤
    │           │            │                │               │              │            │
```

### データ量と実行時間の目安

| ステップ | 処理内容 | 想定データ量 | 目安時間 |
|---|---|---|---|
| Agent 1 → 2 | 構造化ニュース記事 | 1件（JSON 1-2KB） | — |
| Agent 2: KPI取得 | 集計クエリ 3-8本 | 結果行数: 各10-50行 | 2-5秒 |
| Agent 2: スキル実行 | 最大3領域のスコア算出 | 入力パラメータ: 15-25項目 | 1-3秒 |
| Agent 2 → 3 | 影響判定結果 | 1件（JSON 3-5KB） | — |
| Agent 3: 業務データ取得 | 詳細クエリ 3-6本 | 結果行数: 各10-100行 | 3-8秒 |
| Agent 3: レコメンド生成 | 1-3領域分の推奨アクション | 通知テンプレート 1-3件 | 2-5秒 |
| Agent 3 → 4 | 通知内容 | 1-3件（JSON 5-10KB） | — |
| Agent 4: Teams送信 | Adaptive Card 送信 | 1-3メッセージ | 1-2秒 |
| **合計** | | | **10-25秒** |

---

## データ品質・運用ルール

- Agent 2・3 は Fabric テーブルに対して**読取専用**でアクセスする。書込は行わない。
- Agent 2 の集計クエリは Gold 層の KPI テーブルを優先し、存在しない場合のみ Silver/Bronze 層にフォールバックする。
- 個人を特定する粒度での出力は禁止する。Agent 3 のレコメンド出力はセグメント・集計単位にする。
- Agent 2 の判定結果には `data_snapshot`（判断に使用したデータのスナップショット）を含め、判断根拠のトレーサビリティを確保する。
- クエリのタイムアウトは 30 秒とする。タイムアウト時は直近のキャッシュデータで代替判定し、`data_freshness: "cached"` フラグを付与する。
- Agent 4 の Teams 送信が失敗した場合はリトライ（最大3回、30秒間隔）後、フォールバックとしてメール送信を行う。
