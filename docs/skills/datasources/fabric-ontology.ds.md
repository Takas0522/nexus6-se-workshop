# Fabric オントロジー定義

## データソース概要

本オントロジーは、ニュース分析エージェントが3つの事業領域（携帯電話事業・SNS事業・SI事業）に対するインパクト判断を行うために必要なデータモデルを定義する。共通基盤・事業別・分析結果の3層で構成され、Microsoft Fabric上のMedallionアーキテクチャに準拠する。

### データソース一覧

| データベース | 事業領域 | 用途 |
|---|---|---|
| `sqldb_common_01` | 共通基盤 | 統合顧客管理、ニュース分析、通知 |
| `sqldb_mobile_01` | 携帯電話事業 | 契約・利用・MNP管理 |
| `sqldb_ecommerce_01` | SNS事業 | ユーザー・アクティビティ・広告収益 |
| `sqldb_fintech_01` | SI事業 | 法人顧客・プロジェクト・取引管理 |

---

## エンティティ定義

### 共通基盤エンティティ

#### `sqldb_common_01.unified_customers`
**統合顧客マスタ**

全事業領域の顧客を一元管理するハブエンティティ。クロスセル分析・統合インパクト評価の基点。

| カラム | 型 | 説明 |
|---|---|---|
| `unified_customer_id` | UNIQUEIDENTIFIER (PK) | 統合顧客ID |
| `customer_name` | NVARCHAR(200) | 顧客名 |
| `customer_type` | NVARCHAR(20) | 顧客種別（`individual` / `corporate`） |
| `primary_domain` | NVARCHAR(20) | 主所属事業領域 |
| `registered_at` | DATETIME2 | 初回登録日 |
| `status` | NVARCHAR(20) | ステータス（`active` / `inactive` / `churned`） |
| `lifetime_value` | DECIMAL(18,2) | 全事業横断LTV |

#### `sqldb_common_01.domain_id_mappings`
**事業領域間顧客IDマッピング**

統合顧客IDと各事業領域固有IDの対応関係を管理する。

| カラム | 型 | 説明 |
|---|---|---|
| `mapping_id` | UNIQUEIDENTIFIER (PK) | マッピングID |
| `unified_customer_id` | UNIQUEIDENTIFIER (FK) | 統合顧客ID |
| `domain` | NVARCHAR(20) | 事業領域（`mobile` / `sns` / `si`） |
| `domain_customer_id` | NVARCHAR(100) | 事業領域固有の顧客ID |
| `mapped_at` | DATETIME2 | マッピング作成日 |
| `confidence_score` | DECIMAL(5,4) | 名寄せ確信度 |

#### `sqldb_common_01.customer_segments`
**顧客セグメント定義マスタ**

分析・通知のターゲティングに使用するセグメント定義。

| カラム | 型 | 説明 |
|---|---|---|
| `segment_id` | NVARCHAR(50) (PK) | セグメントID |
| `segment_name` | NVARCHAR(100) | セグメント名称 |
| `domain` | NVARCHAR(20) | 対象事業領域（`all` / `mobile` / `sns` / `si`） |
| `criteria_json` | NVARCHAR(MAX) | セグメント条件（JSON） |
| `customer_count` | INT | 対象顧客数（最終更新時） |
| `updated_at` | DATETIME2 | 最終更新日 |

#### `sqldb_common_01.news_articles`
**ニュース記事**

取得・分析対象のニュース記事を格納する。エージェントの入力データ。

| カラム | 型 | 説明 |
|---|---|---|
| `article_id` | UNIQUEIDENTIFIER (PK) | 記事ID |
| `title` | NVARCHAR(500) | 記事タイトル |
| `body` | NVARCHAR(MAX) | 記事本文 |
| `source` | NVARCHAR(200) | ソース媒体名 |
| `published_at` | DATETIME2 | 公開日時 |
| `category` | NVARCHAR(50) | カテゴリ（`economy` / `competition` / `regulation`） |
| `tags` | NVARCHAR(MAX) | タグ（JSON配列） |
| `ingested_at` | DATETIME2 | 取込日時 |

#### `sqldb_common_01.impact_assessments`
**インパクト診断結果**

ニュース記事に対するエージェントの診断結果を格納する。

| カラム | 型 | 説明 |
|---|---|---|
| `assessment_id` | UNIQUEIDENTIFIER (PK) | 診断ID |
| `article_id` | UNIQUEIDENTIFIER (FK) | 対象記事ID |
| `domain` | NVARCHAR(20) | 事業領域 |
| `skill_id` | NVARCHAR(100) | 使用スキルID |
| `impact_score` | INT | インパクトスコア（0-100） |
| `impact_level` | NVARCHAR(20) | インパクトレベル |
| `impact_direction` | NVARCHAR(20) | 影響方向（`positive` / `negative`） |
| `risk_summary` | NVARCHAR(MAX) | リスクサマリー |
| `recommended_actions` | NVARCHAR(MAX) | 推奨アクション（JSON配列） |
| `assessed_at` | DATETIME2 | 診断日時 |

#### `sqldb_common_01.notifications`
**通知履歴**

インパクト診断に基づく各部署への通知履歴。

| カラム | 型 | 説明 |
|---|---|---|
| `notification_id` | UNIQUEIDENTIFIER (PK) | 通知ID |
| `assessment_id` | UNIQUEIDENTIFIER (FK) | 診断ID |
| `recipient_domain` | NVARCHAR(20) | 通知先事業領域 |
| `recipient_role` | NVARCHAR(50) | 通知先ロール |
| `channel` | NVARCHAR(20) | 通知チャネル（`teams` / `email` / `webhook`） |
| `priority` | NVARCHAR(20) | 優先度（`critical` / `high` / `medium` / `low`） |
| `sent_at` | DATETIME2 | 送信日時 |
| `acknowledged_at` | DATETIME2 | 確認日時 |

---

### 携帯電話事業エンティティ

#### `sqldb_mobile_01.customers`
**携帯電話事業顧客**

携帯電話事業における個人・法人顧客情報。

| カラム | 型 | 説明 |
|---|---|---|
| `customer_id` | NVARCHAR(50) (PK) | 携帯顧客ID |
| `customer_name` | NVARCHAR(200) | 顧客名 |
| `customer_type` | NVARCHAR(20) | 種別（`individual` / `corporate`） |
| `registered_at` | DATETIME2 | 登録日 |
| `segment_id` | NVARCHAR(50) (FK) | セグメントID |
| `prefecture` | NVARCHAR(10) | 都道府県 |
| `status` | NVARCHAR(20) | ステータス |

#### `sqldb_mobile_01.contracts`
**携帯電話契約**

回線契約・端末割賦契約の情報。

| カラム | 型 | 説明 |
|---|---|---|
| `contract_id` | NVARCHAR(50) (PK) | 契約ID |
| `customer_id` | NVARCHAR(50) (FK) | 顧客ID |
| `plan_id` | NVARCHAR(50) | 料金プランID |
| `terminal_id` | NVARCHAR(50) | 端末ID |
| `contract_start` | DATE | 契約開始日 |
| `contract_end` | DATE | 契約終了日 |
| `installment_flag` | BIT | 割賦販売フラグ |
| `installment_months` | INT | 分割回数 |
| `monthly_fee` | DECIMAL(10,2) | 月額料金 |
| `status` | NVARCHAR(20) | 契約ステータス |

#### `sqldb_mobile_01.usage_billing`
**利用・課金履歴**

月次の利用実績と課金情報。

| カラム | 型 | 説明 |
|---|---|---|
| `billing_id` | UNIQUEIDENTIFIER (PK) | 課金ID |
| `contract_id` | NVARCHAR(50) (FK) | 契約ID |
| `billing_month` | DATE | 課金月 |
| `data_usage_gb` | DECIMAL(10,3) | データ使用量(GB) |
| `voice_minutes` | INT | 通話分数 |
| `total_charge` | DECIMAL(10,2) | 合計請求額 |
| `payment_status` | NVARCHAR(20) | 入金ステータス |

#### `sqldb_mobile_01.mnp_history`
**MNP履歴**

番号ポータビリティによる転入・転出履歴。解約予測・競合分析に使用。

| カラム | 型 | 説明 |
|---|---|---|
| `mnp_id` | UNIQUEIDENTIFIER (PK) | MNP ID |
| `customer_id` | NVARCHAR(50) (FK) | 顧客ID |
| `direction` | NVARCHAR(10) | 方向（`in` / `out`） |
| `carrier_from` | NVARCHAR(50) | 転出元キャリア |
| `carrier_to` | NVARCHAR(50) | 転入先キャリア |
| `reason` | NVARCHAR(100) | 転出理由 |
| `processed_at` | DATETIME2 | 処理日時 |

---

### SNS事業エンティティ

#### `sqldb_ecommerce_01.sns_users`
**SNSユーザー**

SNSプラットフォームのユーザー情報。

| カラム | 型 | 説明 |
|---|---|---|
| `user_id` | NVARCHAR(50) (PK) | ユーザーID |
| `display_name` | NVARCHAR(100) | 表示名 |
| `user_type` | NVARCHAR(20) | 種別（`general` / `creator` / `business`） |
| `registered_at` | DATETIME2 | 登録日 |
| `last_active_at` | DATETIME2 | 最終アクティブ日時 |
| `follower_count` | INT | フォロワー数 |
| `subscription_plan` | NVARCHAR(20) | 課金プラン（`free` / `premium`） |
| `status` | NVARCHAR(20) | ステータス |

#### `sqldb_ecommerce_01.sns_activities`
**SNSアクティビティ履歴**

投稿・いいね・シェア等のユーザーアクション履歴。

| カラム | 型 | 説明 |
|---|---|---|
| `activity_id` | UNIQUEIDENTIFIER (PK) | アクティビティID |
| `user_id` | NVARCHAR(50) (FK) | ユーザーID |
| `activity_type` | NVARCHAR(20) | 種別（`post` / `like` / `share` / `comment`） |
| `content_id` | NVARCHAR(50) | 対象コンテンツID |
| `created_at` | DATETIME2 | 発生日時 |
| `platform` | NVARCHAR(20) | プラットフォーム（`web` / `ios` / `android`） |

#### `sqldb_ecommerce_01.ad_revenues`
**SNS広告収益**

広告キャンペーン単位の収益情報。

| カラム | 型 | 説明 |
|---|---|---|
| `revenue_id` | UNIQUEIDENTIFIER (PK) | 収益ID |
| `advertiser_id` | NVARCHAR(50) | 広告主ID |
| `campaign_id` | NVARCHAR(50) | キャンペーンID |
| `revenue_date` | DATE | 計上日 |
| `impressions` | BIGINT | インプレッション数 |
| `clicks` | INT | クリック数 |
| `revenue_jpy` | DECIMAL(12,2) | 収益額（円） |
| `currency` | NVARCHAR(10) | 請求通貨 |
| `advertiser_industry` | NVARCHAR(50) | 広告主業種 |

---

### SI事業エンティティ

#### `sqldb_fintech_01.si_clients`
**SI事業顧客（法人）**

SI事業のクライアント企業情報。

| カラム | 型 | 説明 |
|---|---|---|
| `client_id` | NVARCHAR(50) (PK) | 法人顧客ID |
| `company_name` | NVARCHAR(200) | 企業名 |
| `industry` | NVARCHAR(50) | 業種 |
| `employee_count` | INT | 従業員数 |
| `annual_it_budget_oku` | DECIMAL(10,2) | 年間IT予算（億円） |
| `tier` | NVARCHAR(10) | 顧客ティア（`S` / `A` / `B` / `C`） |
| `relationship_start` | DATE | 取引開始日 |
| `status` | NVARCHAR(20) | ステータス |

#### `sqldb_fintech_01.si_projects`
**SIプロジェクト**

SI案件のプロジェクト情報。パイプライン〜完了まで管理。

| カラム | 型 | 説明 |
|---|---|---|
| `project_id` | NVARCHAR(50) (PK) | プロジェクトID |
| `client_id` | NVARCHAR(50) (FK) | 法人顧客ID |
| `project_name` | NVARCHAR(200) | プロジェクト名 |
| `project_type` | NVARCHAR(30) | 種別（`new_development` / `migration` / `maintenance` / `consulting`） |
| `contract_type` | NVARCHAR(20) | 契約形態（`fixed_price` / `time_and_material`） |
| `amount_oku` | DECIMAL(10,2) | 案件金額（億円） |
| `currency` | NVARCHAR(10) | 契約通貨 |
| `start_date` | DATE | 開始日 |
| `planned_end_date` | DATE | 予定完了日 |
| `status` | NVARCHAR(20) | ステータス（`pipeline` / `active` / `on_hold` / `completed` / `cancelled`） |
| `offshore_ratio` | DECIMAL(5,4) | オフショア比率 |

#### `sqldb_fintech_01.si_transactions`
**SI取引・請求履歴**

プロジェクトに紐づく請求・入金履歴。

| カラム | 型 | 説明 |
|---|---|---|
| `transaction_id` | UNIQUEIDENTIFIER (PK) | 取引ID |
| `project_id` | NVARCHAR(50) (FK) | プロジェクトID |
| `transaction_type` | NVARCHAR(20) | 種別（`invoice` / `payment` / `adjustment`） |
| `amount_jpy` | DECIMAL(14,2) | 金額（円） |
| `currency` | NVARCHAR(10) | 通貨 |
| `transaction_date` | DATE | 取引日 |
| `due_date` | DATE | 支払期日 |
| `status` | NVARCHAR(20) | ステータス |

---

## リレーション定義

```
sqldb_common_01.unified_customers
    │
    ├──< sqldb_common_01.domain_id_mappings (1:N)
    │       │
    │       ├── domain="mobile" → sqldb_mobile_01.customers
    │       ├── domain="sns"    → sqldb_ecommerce_01.sns_users
    │       └── domain="si"     → sqldb_fintech_01.si_clients
    │
    └──< sqldb_common_01.customer_segments (N:1, via segment_id)

sqldb_mobile_01.customers
    └──< sqldb_mobile_01.contracts (1:N)
            ├──< sqldb_mobile_01.usage_billing (1:N)
            └──< sqldb_mobile_01.mnp_history (via customer_id)

sqldb_ecommerce_01.sns_users
    └──< sqldb_ecommerce_01.sns_activities (1:N)

sqldb_ecommerce_01.ad_revenues
    (独立: advertiser_id による広告主単位集計)

sqldb_fintech_01.si_clients
    └──< sqldb_fintech_01.si_projects (1:N)
            └──< sqldb_fintech_01.si_transactions (1:N)

sqldb_common_01.news_articles
    └──< sqldb_common_01.impact_assessments (1:N)
            └──< sqldb_common_01.notifications (1:N)
```

### 主要結合パス

| 結合目的 | FROM | JOIN | ON |
|---|---|---|---|
| 統合顧客→携帯契約 | `unified_customers` | `domain_id_mappings` → `mobile_01.customers` → `contracts` | `unified_customer_id` → `domain_customer_id` = `customer_id` |
| 統合顧客→SNS活動 | `unified_customers` | `domain_id_mappings` → `ecommerce_01.sns_users` → `sns_activities` | `unified_customer_id` → `domain_customer_id` = `user_id` |
| 統合顧客→SI案件 | `unified_customers` | `domain_id_mappings` → `fintech_01.si_clients` → `si_projects` | `unified_customer_id` → `domain_customer_id` = `client_id` |
| ニュース→診断→通知 | `news_articles` | `impact_assessments` → `notifications` | `article_id` → `assessment_id` |

---

## メダリオン構成

### Bronze層（Raw）

生データをそのまま取り込むレイヤー。ソースシステムからの増分取込を管理。

| Lakehouse テーブル | ソース | 取込頻度 |
|---|---|---|
| `bronze_mobile_customers` | `sqldb_mobile_01.customers` | 日次 |
| `bronze_mobile_contracts` | `sqldb_mobile_01.contracts` | 日次 |
| `bronze_mobile_usage_billing` | `sqldb_mobile_01.usage_billing` | 日次 |
| `bronze_mobile_mnp_history` | `sqldb_mobile_01.mnp_history` | 日次 |
| `bronze_sns_users` | `sqldb_ecommerce_01.sns_users` | 日次 |
| `bronze_sns_activities` | `sqldb_ecommerce_01.sns_activities` | 時間次 |
| `bronze_sns_ad_revenues` | `sqldb_ecommerce_01.ad_revenues` | 日次 |
| `bronze_si_clients` | `sqldb_fintech_01.si_clients` | 日次 |
| `bronze_si_projects` | `sqldb_fintech_01.si_projects` | 日次 |
| `bronze_si_transactions` | `sqldb_fintech_01.si_transactions` | 日次 |
| `bronze_news_articles` | `sqldb_common_01.news_articles` | リアルタイム |
| `bronze_impact_assessments` | `sqldb_common_01.impact_assessments` | リアルタイム |

### Silver層（Cleansed & Enriched）

データクレンジング、正規化、エンリッチメントを適用したレイヤー。

| Lakehouse テーブル | 変換内容 |
|---|---|
| `silver_unified_customers` | 名寄せ済み統合顧客（全事業領域のID付与） |
| `silver_mobile_contracts_enriched` | 契約＋プラン詳細＋端末情報結合 |
| `silver_mobile_churn_signals` | 解約予兆スコア付与（MNP履歴＋利用量推移） |
| `silver_sns_user_engagement` | エンゲージメントスコア算出（DAU/MAU, セッション等） |
| `silver_sns_ad_performance` | 広告KPI算出（CTR, CPC, ROAS） |
| `silver_si_project_health` | プロジェクト健全性スコア（進捗・収益・リスク） |
| `silver_si_pipeline_scored` | パイプライン案件の受注確度スコア付与 |
| `silver_news_classified` | ニュース記事のカテゴリ分類・エンティティ抽出済み |

### Gold層（Business-Ready）

ビジネスユーザー・エージェントが直接利用する集計・分析テーブル。

| Lakehouse テーブル | 用途 |
|---|---|
| `gold_mobile_kpi_monthly` | 携帯事業月次KPI集計 |
| `gold_mobile_arpu_trend` | ARPU推移分析 |
| `gold_mobile_churn_forecast` | 解約予測サマリー |
| `gold_sns_kpi_daily` | SNS事業日次KPI集計 |
| `gold_sns_ad_revenue_forecast` | 広告収益予測 |
| `gold_sns_creator_ecosystem` | クリエイターエコシステム指標 |
| `gold_si_pipeline_summary` | SIパイプライン状況サマリー |
| `gold_si_project_profitability` | プロジェクト収益性分析 |
| `gold_si_client_health` | 顧客ヘルススコア |
| `gold_cross_domain_impact` | 事業横断インパクト集計 |
| `gold_news_impact_dashboard` | ニュースインパクトダッシュボード用 |

---

## KPI定義（Gold層集計テーブル）

### 携帯電話事業KPI (`gold_mobile_kpi_monthly`)

| KPI | 算出ロジック | 単位 |
|---|---|---|
| `subscriber_count` | 月末時点のアクティブ契約数 | 件 |
| `arpu` | 月間売上 / アクティブ契約数 | 円 |
| `churn_rate` | 月間解約数 / 月初契約数 | % |
| `mnp_out_count` | MNP転出件数 | 件 |
| `mnp_in_count` | MNP転入件数 | 件 |
| `installment_active_ratio` | 割賦契約中比率 | % |
| `data_usage_avg_gb` | 平均データ利用量 | GB |

### SNS事業KPI (`gold_sns_kpi_daily`)

| KPI | 算出ロジック | 単位 |
|---|---|---|
| `dau` | 日次アクティブユーザー数 | 人 |
| `mau` | 月次アクティブユーザー数 | 人 |
| `dau_mau_ratio` | DAU / MAU | % |
| `ad_revenue_daily` | 日次広告収入合計 | 円 |
| `ad_ctr` | クリック数 / インプレッション数 | % |
| `premium_conversion_rate` | 有料プラン転換率 | % |
| `creator_active_count` | アクティブクリエイター数 | 人 |
| `avg_session_duration` | 平均セッション時間 | 分 |

### SI事業KPI (`gold_si_pipeline_summary`)

| KPI | 算出ロジック | 単位 |
|---|---|---|
| `pipeline_total_oku` | パイプライン案件合計金額 | 億円 |
| `pipeline_count` | パイプライン案件数 | 件 |
| `active_project_count` | 進行中プロジェクト数 | 件 |
| `avg_project_margin` | 平均プロジェクト利益率 | % |
| `utilization_rate` | 技術者稼働率 | % |
| `on_hold_count` | 凍結中案件数 | 件 |
| `quarterly_revenue_oku` | 四半期売上 | 億円 |
| `bid_win_rate` | 入札勝率 | % |

### 事業横断KPI (`gold_cross_domain_impact`)

| KPI | 算出ロジック | 単位 |
|---|---|---|
| `total_impact_assessments` | 期間中のインパクト診断実行数 | 件 |
| `critical_alerts_count` | critical判定数 | 件 |
| `avg_response_time_hours` | 通知→確認の平均時間 | 時間 |
| `cross_sell_opportunity_count` | 事業横断クロスセル検出数 | 件 |
| `multi_domain_customer_ratio` | 複数事業利用顧客比率 | % |
