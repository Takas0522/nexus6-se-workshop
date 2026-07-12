# Fabric オントロジー定義

## データソース概要

本システムは、3つの業務領域（携帯電話事業・SNS事業・SI事業）と統合顧客基盤で構成されるマルチドメインデータモデルを Microsoft Fabric 上で管理する。ニュース分析エージェントがビジネスインパクトを診断するために、各レイヤーのデータを横断的に参照する。

### データベース構成

| データベース | 業務領域 | 説明 |
|---|---|---|
| `sqldb_common_01` | 共通基盤 | 統合顧客マスタ、ドメイン間ID紐付け、セグメント |
| `sqldb_mobile_01` | 携帯電話事業 | 顧客、契約、利用/課金トランザクション、端末在庫 |
| `sqldb_sns_01` | SNS事業 | ユーザー、有料プラン、広告/課金トランザクション、広告枠 |
| `sqldb_si_01` | SI事業 | 法人顧客、案件契約、取引/請求、リソース在庫 |
| `sqldb_news_01` | ニュース分析 | ニュース記事、インパクト診断結果、通知履歴 |

---

## エンティティ定義

### 共通基盤（sqldb_common_01）

#### unified_customers — 統合顧客マスタ

全業務領域の顧客を一意に管理する統合エンティティ。クロスドメイン分析の起点。

| カラム | 型 | 説明 |
|---|---|---|
| `unified_customer_id` | UNIQUEIDENTIFIER | 統合顧客ID（PK） |
| `customer_name` | NVARCHAR(200) | 顧客名 |
| `email` | NVARCHAR(256) | メールアドレス |
| `phone` | NVARCHAR(20) | 電話番号 |
| `created_at` | DATETIME2 | 登録日時 |
| `updated_at` | DATETIME2 | 更新日時 |

**ビジネス意味**: 企業グループ全体で顧客を統合的に把握し、ドメイン横断のインパクト分析を可能にする中核エンティティ。

#### domain_id_mappings — 業務領域間ID紐付け

統合顧客IDと各業務領域の固有顧客IDをマッピングするブリッジエンティティ。

| カラム | 型 | 説明 |
|---|---|---|
| `mapping_id` | UNIQUEIDENTIFIER | マッピングID（PK） |
| `unified_customer_id` | UNIQUEIDENTIFIER | 統合顧客ID（FK） |
| `domain` | NVARCHAR(50) | 業務領域（`mobile` / `sns` / `si`） |
| `domain_customer_id` | NVARCHAR(100) | 領域固有顧客ID |

**ビジネス意味**: 1人の顧客が複数領域を利用する場合のID解決に使用。インパクト分析時に「ニュースAが影響する携帯顧客のうち、SNSも利用している人数」等を算出。

#### customer_segments — 顧客セグメント分類

顧客の行動・属性に基づくセグメント分類。通知優先度やインパクト規模の推定に利用。

| カラム | 型 | 説明 |
|---|---|---|
| `unified_customer_id` | UNIQUEIDENTIFIER | 統合顧客ID（FK） |
| `segment_code` | NVARCHAR(20) | セグメントコード |
| `segment_name` | NVARCHAR(100) | セグメント名 |
| `assigned_at` | DATETIME2 | 分類日時 |

**ビジネス意味**: VIP/一般/休眠等のセグメントにより、インパクト通知の優先度と内容をパーソナライズ。

---

### 携帯電話事業（sqldb_mobile_01）

#### customers — 携帯電話事業顧客

携帯電話サービスの契約者マスタ。

| カラム | 型 | 説明 |
|---|---|---|
| `customer_id` | NVARCHAR(100) | 顧客ID（PK, `MOB-XXXXXX`形式） |
| `plan_type` | NVARCHAR(50) | プラン種別 |
| `status` | NVARCHAR(20) | ステータス（`active`/`suspended`/`churned`） |
| `created_at` | DATETIME2 | 登録日時 |

**ビジネス意味**: MNP転出リスク分析、プラン変更トレンド、解約予測の基礎データ。

#### contracts — 携帯電話契約

回線・端末・オプション等の個別契約。

| カラム | 型 | 説明 |
|---|---|---|
| `contract_id` | UNIQUEIDENTIFIER | 契約ID（PK） |
| `customer_id` | NVARCHAR(100) | 顧客ID（FK） |
| `plan_code` | NVARCHAR(30) | プランコード |
| `start_date` | DATE | 契約開始日 |
| `end_date` | DATE | 契約終了日 |
| `monthly_fee` | DECIMAL(10,2) | 月額料金 |

**ビジネス意味**: 端末割賦残債・更新期顧客の把握。在庫不足時の機種変更抑制シミュレーションに使用。

#### transactions — 携帯電話利用・課金トランザクション

通話/通信/購入/課金等の取引記録。

| カラム | 型 | 説明 |
|---|---|---|
| `transaction_id` | UNIQUEIDENTIFIER | トランザクションID（PK） |
| `customer_id` | NVARCHAR(100) | 顧客ID（FK） |
| `transaction_type` | NVARCHAR(30) | 取引種別（`usage`/`purchase`/`payment`/`refund`） |
| `amount` | DECIMAL(12,2) | 金額 |
| `transaction_date` | DATETIME2 | 取引日時 |

**ビジネス意味**: 月間ARPU、利用傾向、解約前兆の分析基盤。6ヶ月分108,000件。

#### inventory — 端末在庫

端末のモデル別・倉庫別在庫状況。

| カラム | 型 | 説明 |
|---|---|---|
| `inventory_id` | UNIQUEIDENTIFIER | 在庫ID（PK） |
| `device_model` | NVARCHAR(100) | 端末モデル名 |
| `quantity` | INT | 在庫数 |
| `warehouse_code` | NVARCHAR(20) | 倉庫コード |
| `updated_at` | DATETIME2 | 更新日時 |

**ビジネス意味**: 半導体供給不足シナリオでの在庫逼迫度・販売機会損失の判断に直結。

---

### SNS事業（sqldb_sns_01）

#### customers — SNS事業ユーザー

SNSプラットフォームの登録ユーザーマスタ。

| カラム | 型 | 説明 |
|---|---|---|
| `customer_id` | NVARCHAR(100) | ユーザーID（PK, `SNS-XXXXXX`形式） |
| `username` | NVARCHAR(100) | ユーザー名 |
| `account_tier` | NVARCHAR(20) | アカウント種別（`free`/`premium`/`business`） |
| `created_at` | DATETIME2 | 登録日時 |

**ビジネス意味**: DAU/MAU算出、課金ユーザー比率、アカウント成長分析の基礎。

#### contracts — SNS有料プラン契約

プレミアムプラン・ビジネスプラン等の有料サブスクリプション。

| カラム | 型 | 説明 |
|---|---|---|
| `contract_id` | UNIQUEIDENTIFIER | 契約ID（PK） |
| `customer_id` | NVARCHAR(100) | ユーザーID（FK） |
| `plan_code` | NVARCHAR(30) | プランコード |
| `start_date` | DATE | 開始日 |
| `monthly_fee` | DECIMAL(10,2) | 月額料金 |

**ビジネス意味**: サブスクリプション収益の安定性評価。規制変更時の解約リスク分析。

#### transactions — SNS広告・課金トランザクション

広告収入・課金アイテム購入・サブスク決済等の取引記録。

| カラム | 型 | 説明 |
|---|---|---|
| `transaction_id` | UNIQUEIDENTIFIER | トランザクションID（PK） |
| `customer_id` | NVARCHAR(100) | ユーザーID（FK） |
| `transaction_type` | NVARCHAR(30) | 取引種別（`ad_revenue`/`subscription`/`in_app_purchase`/`boost`） |
| `amount` | DECIMAL(12,2) | 金額 |
| `transaction_date` | DATETIME2 | 取引日時 |

**ビジネス意味**: 広告収益モデルへの依存度分析。DSA規制シナリオでの収益インパクト算出基盤。6ヶ月分270,000件。

#### ad_inventory — 広告枠在庫

広告インプレッション枠の種別・在庫・単価。

| カラム | 型 | 説明 |
|---|---|---|
| `ad_slot_id` | UNIQUEIDENTIFIER | 広告枠ID（PK） |
| `slot_type` | NVARCHAR(50) | 枠種別（`banner`/`interstitial`/`video`/`native`） |
| `available_impressions` | INT | 利用可能インプレッション数 |
| `unit_price` | DECIMAL(10,2) | 単価（CPM） |
| `updated_at` | DATETIME2 | 更新日時 |

**ビジネス意味**: 広告収益予測と規制影響シミュレーション。DSA適用時のターゲティング制限による単価下落の定量化。

---

### SI事業（sqldb_si_01）

#### customers — SI事業顧客（法人）

システムインテグレーション事業の法人顧客マスタ。

| カラム | 型 | 説明 |
|---|---|---|
| `customer_id` | NVARCHAR(100) | 顧客ID（PK, `SI-XXXXXX`形式） |
| `company_name` | NVARCHAR(200) | 法人名 |
| `industry` | NVARCHAR(50) | 業種 |
| `created_at` | DATETIME2 | 登録日時 |

**ビジネス意味**: 業種別のニュースインパクト集計。通信事業者顧客の特定（規制対応需要分析）。

#### contracts — SI案件契約

プロジェクト単位の契約情報。

| カラム | 型 | 説明 |
|---|---|---|
| `contract_id` | UNIQUEIDENTIFIER | 契約ID（PK） |
| `customer_id` | NVARCHAR(100) | 顧客ID（FK） |
| `project_name` | NVARCHAR(200) | プロジェクト名 |
| `contract_value` | DECIMAL(14,2) | 契約金額 |
| `start_date` | DATE | 開始日 |
| `end_date` | DATE | 終了日 |

**ビジネス意味**: ニュースインパクトの金額規模推定（影響を受けるPJ数×契約額）。納期リスクの算出基盤。

#### transactions — SI事業取引・請求トランザクション

マイルストーン請求・月次請求・追加作業等の取引記録。

| カラム | 型 | 説明 |
|---|---|---|
| `transaction_id` | UNIQUEIDENTIFIER | トランザクションID（PK） |
| `customer_id` | NVARCHAR(100) | 顧客ID（FK） |
| `contract_id` | UNIQUEIDENTIFIER | 契約ID（FK） |
| `transaction_type` | NVARCHAR(30) | 取引種別（`milestone`/`monthly`/`change_order`/`penalty`） |
| `amount` | DECIMAL(14,2) | 金額 |
| `transaction_date` | DATETIME2 | 取引日時 |

**ビジネス意味**: 収益認識タイミングと遅延リスクの可視化。6ヶ月分162,000件。

#### inventory — SI事業リソース在庫（要員・ライセンス）

エンジニア要員プール、ソフトウェアライセンス、HW在庫の管理。

| カラム | 型 | 説明 |
|---|---|---|
| `inventory_id` | UNIQUEIDENTIFIER | 在庫ID（PK） |
| `resource_type` | NVARCHAR(50) | リソース種別（`engineer`/`license`/`hardware`） |
| `resource_name` | NVARCHAR(100) | リソース名 |
| `available_count` | INT | 利用可能数 |
| `updated_at` | DATETIME2 | 更新日時 |

**ビジネス意味**: 半導体不足シナリオでのHW調達制約、通信品質基準シナリオでのエンジニア充足度の判断材料。

---

### ニュース分析基盤（sqldb_news_01）

#### news_articles — ニュース記事

取得・解析対象のニュース記事。外部ソースからフェッチされた原文データ。

| カラム | 型 | 説明 |
|---|---|---|
| `article_id` | UNIQUEIDENTIFIER | 記事ID（PK） |
| `title` | NVARCHAR(500) | 記事タイトル |
| `summary` | NVARCHAR(2000) | 記事概要 |
| `source` | NVARCHAR(200) | ソース名（媒体名） |
| `source_url` | NVARCHAR(1000) | ソースURL |
| `category` | NVARCHAR(50) | カテゴリ（`regulation` / `economy` / `technology` / `security`） |
| `published_at` | DATETIME2 | 記事公開日時 |
| `fetched_at` | DATETIME2 | フェッチ日時 |

**ビジネス意味**: インパクト分析のトリガーとなる入力データ。カテゴリ・公開日時に基づいてスキルの選択と優先度が決定される。

#### impact_analyses — インパクト診断結果

AIエージェント（スキル）によるニュース記事のインパクト診断結果。1記事×1ドメインで1レコード。

| カラム | 型 | 説明 |
|---|---|---|
| `analysis_id` | UNIQUEIDENTIFIER | 分析ID（PK） |
| `article_id` | UNIQUEIDENTIFIER | 記事ID（FK → news_articles） |
| `domain` | NVARCHAR(30) | 影響対象ドメイン（`mobile` / `sns` / `si`） |
| `impact_level` | NVARCHAR(10) | インパクトレベル（`critical` / `high` / `medium` / `low` / `minimal`） |
| `impact_summary` | NVARCHAR(2000) | インパクト概要（自然言語） |
| `confidence_score` | DECIMAL(3,2) | 確信度スコア（0.00-1.00） |
| `affected_customer_count` | INT | 影響顧客数 |
| `estimated_revenue_impact` | DECIMAL(15,2) | 推定収益影響額（円） |
| `analyzed_at` | DATETIME2 | 分析実行日時 |

**ビジネス意味**: スキルの出力結果を永続化。通知生成・ダッシュボード表示・過去インパクトとの比較分析に使用。同一記事を複数ドメインで分析した結果を保持し、クロスドメインの影響を可視化。

#### notifications — 通知履歴

インパクト診断結果に基づいて各部署へ送信された通知の履歴。

| カラム | 型 | 説明 |
|---|---|---|
| `notification_id` | UNIQUEIDENTIFIER | 通知ID（PK） |
| `analysis_id` | UNIQUEIDENTIFIER | 分析ID（FK → impact_analyses） |
| `domain` | NVARCHAR(30) | 通知先ドメイン |
| `channel` | NVARCHAR(30) | 通知チャネル（`teams` / `email` / `push` / `dashboard`） |
| `title` | NVARCHAR(500) | 通知タイトル |
| `body` | NVARCHAR(4000) | 通知本文 |
| `priority` | NVARCHAR(10) | 優先度（`P0` / `P1` / `P2` / `P3`） |
| `status` | NVARCHAR(20) | ステータス（`sent` / `delivered` / `read` / `acknowledged` / `failed`） |
| `sent_at` | DATETIME2 | 送信日時 |
| `read_at` | DATETIME2 | 既読日時（NULL許容） |

**ビジネス意味**: 通知の到達・既読状況を追跡。重複通知の排除、エスカレーション判定（P0が未読のまま30分経過→再通知）、通知効果の分析に使用。

---

## リレーション定義

### エンティティ関係図（ER概念）

```
┌─────────────────────────────────────────────────────────────────────┐
│                        sqldb_common_01                               │
│                                                                     │
│  unified_customers ──1:N──> domain_id_mappings                      │
│        │                         │                                  │
│        └──1:1──> customer_segments                                  │
│                                  │                                  │
└──────────────────────────────────┼──────────────────────────────────┘
                                   │
                   domain_customer_id（論理FK）
                                   │
         ┌─────────────────────────┼──────────────────────────┐
         │                         │                          │
         ▼                         ▼                          ▼
┌─────────────────┐   ┌─────────────────┐   ┌─────────────────────┐
│  sqldb_mobile_01 │   │   sqldb_sns_01   │   │    sqldb_si_01       │
│                 │   │                 │   │                     │
│ customers       │   │ customers       │   │ customers           │
│   │             │   │   │             │   │   │                 │
│   ├─1:N─contracts│   │   ├─1:N─contracts│   │   ├─1:N─contracts   │
│   │             │   │   │             │   │   │   │             │
│   └─1:N─transactions│   └─1:N─transactions│   │   └─1:N─transactions│
│                 │   │                 │   │                     │
│ inventory(独立) │   │ ad_inventory(独立)│   │ inventory(独立)     │
└─────────────────┘   └─────────────────┘   └─────────────────────┘
```

### リレーション一覧

| # | 起点エンティティ | 関係 | 終点エンティティ | 結合キー | 結合種別 |
|---|---|---|---|---|---|
| 1 | unified_customers | 1:N | domain_id_mappings | `unified_customer_id` | 物理FK |
| 2 | unified_customers | 1:1 | customer_segments | `unified_customer_id` | 物理FK |
| 3 | domain_id_mappings | 1:1 | mobile.customers | `domain_customer_id = customer_id` (WHERE domain='mobile') | 論理FK |
| 4 | domain_id_mappings | 1:1 | sns.customers | `domain_customer_id = customer_id` (WHERE domain='sns') | 論理FK |
| 5 | domain_id_mappings | 1:1 | si.customers | `domain_customer_id = customer_id` (WHERE domain='si') | 論理FK |
| 6 | mobile.customers | 1:N | mobile.contracts | `customer_id` | 物理FK |
| 7 | mobile.customers | 1:N | mobile.transactions | `customer_id` | 物理FK |
| 8 | sns.customers | 1:N | sns.contracts | `customer_id` | 物理FK |
| 9 | sns.customers | 1:N | sns.transactions | `customer_id` | 物理FK |
| 10 | si.customers | 1:N | si.contracts | `customer_id` | 物理FK |
| 11 | si.customers | 1:N | si.transactions | `customer_id` | 物理FK |
| 12 | si.contracts | 1:N | si.transactions | `contract_id` | 物理FK |
| 13 | news.news_articles | 1:N | news.impact_analyses | `article_id` | 物理FK |
| 14 | news.impact_analyses | 1:N | news.notifications | `analysis_id` | 物理FK |

### クロスドメイン結合パターン

```sql
-- 統合顧客から全ドメインの取引を横断集計
SELECT uc.unified_customer_id, uc.customer_name,
       SUM(CASE WHEN dm.domain = 'mobile' THEN mt.amount ELSE 0 END) AS mobile_total,
       SUM(CASE WHEN dm.domain = 'sns' THEN st.amount ELSE 0 END) AS sns_total,
       SUM(CASE WHEN dm.domain = 'si' THEN it.amount ELSE 0 END) AS si_total
FROM sqldb_common_01.unified_customers uc
JOIN sqldb_common_01.domain_id_mappings dm ON uc.unified_customer_id = dm.unified_customer_id
LEFT JOIN sqldb_mobile_01.transactions mt ON dm.domain_customer_id = mt.customer_id AND dm.domain = 'mobile'
LEFT JOIN sqldb_sns_01.transactions st ON dm.domain_customer_id = st.customer_id AND dm.domain = 'sns'
LEFT JOIN sqldb_si_01.transactions it ON dm.domain_customer_id = it.customer_id AND dm.domain = 'si'
GROUP BY uc.unified_customer_id, uc.customer_name;
```

---

## メダリオン構成（Bronze / Silver / Gold）

### Bronze 層 — Raw Ingestion

ソースDBからの差分・全量取り込み。変換なし、到着順保持。

| Lakehouse テーブル | ソース | 更新頻度 | 形式 |
|---|---|---|---|
| `bronze_common_customers` | sqldb_common_01.unified_customers | 日次 | Delta |
| `bronze_common_mappings` | sqldb_common_01.domain_id_mappings | 日次 | Delta |
| `bronze_common_segments` | sqldb_common_01.customer_segments | 日次 | Delta |
| `bronze_mobile_customers` | sqldb_mobile_01.customers | 日次 | Delta |
| `bronze_mobile_contracts` | sqldb_mobile_01.contracts | 日次 | Delta |
| `bronze_mobile_transactions` | sqldb_mobile_01.transactions | 時間次 | Delta |
| `bronze_mobile_inventory` | sqldb_mobile_01.inventory | 時間次 | Delta |
| `bronze_sns_customers` | sqldb_sns_01.customers | 日次 | Delta |
| `bronze_sns_contracts` | sqldb_sns_01.contracts | 日次 | Delta |
| `bronze_sns_transactions` | sqldb_sns_01.transactions | 時間次 | Delta |
| `bronze_sns_ad_inventory` | sqldb_sns_01.ad_inventory | 時間次 | Delta |
| `bronze_si_customers` | sqldb_si_01.customers | 日次 | Delta |
| `bronze_si_contracts` | sqldb_si_01.contracts | 日次 | Delta |
| `bronze_si_transactions` | sqldb_si_01.transactions | 時間次 | Delta |
| `bronze_si_inventory` | sqldb_si_01.inventory | 日次 | Delta |
| `bronze_news_articles` | sqldb_news_01.news_articles | リアルタイム | Delta |
| `bronze_news_impact_analyses` | sqldb_news_01.impact_analyses | リアルタイム | Delta |
| `bronze_news_notifications` | sqldb_news_01.notifications | リアルタイム | Delta |

### Silver 層 — Cleansed & Conformed

データ品質チェック済み、型統一、重複排除、SCD Type2 適用。

| Lakehouse テーブル | 説明 | 主な変換 |
|---|---|---|
| `silver_dim_customers_unified` | 統合顧客ディメンション | 全ドメイン顧客を統合、SCD2 |
| `silver_dim_customers_mobile` | 携帯事業顧客ディメンション | status正規化、NULL補完 |
| `silver_dim_customers_sns` | SNS事業顧客ディメンション | tier正規化 |
| `silver_dim_customers_si` | SI事業顧客ディメンション | industry正規化 |
| `silver_dim_contracts_mobile` | 携帯契約ディメンション | 契約期間計算、ステータス付与 |
| `silver_dim_contracts_sns` | SNS契約ディメンション | MRR計算付与 |
| `silver_dim_contracts_si` | SI契約ディメンション | 進捗率計算付与 |
| `silver_fact_transactions_mobile` | 携帯トランザクションファクト | 金額正規化（税別統一）、分類コード付与 |
| `silver_fact_transactions_sns` | SNSトランザクションファクト | 広告/課金分類、通貨統一 |
| `silver_fact_transactions_si` | SIトランザクションファクト | マイルストーン/月次分類 |
| `silver_dim_inventory_mobile` | 端末在庫ディメンション | モデル正規化、カテゴリ付与 |
| `silver_dim_inventory_sns` | 広告枠ディメンション | 枠種別正規化、eCPM計算 |
| `silver_dim_inventory_si` | リソース在庫ディメンション | リソース種別正規化 |
| `silver_bridge_domain_mappings` | ドメイン間ブリッジ | 有効マッピングのみ抽出 |
| `silver_dim_segments` | セグメントディメンション | コード→名称正規化 |
| `silver_fact_news_articles` | ニュース記事ファクト | カテゴリ正規化、重複排除、言語検出 |
| `silver_fact_impact_analyses` | インパクト診断ファクト | スコア正規化、ドメイン名統一 |
| `silver_fact_notifications` | 通知履歴ファクト | ステータス正規化、配信遅延計算 |

### Gold 層 — Business-Ready Aggregates

ビジネスKPI・インパクト分析に最適化されたスタースキーマ / 集計テーブル。

| Lakehouse テーブル | 説明 | 粒度 | 更新頻度 |
|---|---|---|---|
| `gold_agg_revenue_by_domain` | ドメイン別月次収益 | 月×ドメイン | 日次 |
| `gold_agg_customer_lifetime_value` | 顧客LTV | 顧客 | 週次 |
| `gold_agg_churn_risk_scores` | 解約リスクスコア | 顧客×月 | 日次 |
| `gold_agg_mobile_arpu` | 携帯ARPU | 月×プラン | 日次 |
| `gold_agg_mobile_inventory_health` | 端末在庫健全性 | モデル×倉庫×日 | 時間次 |
| `gold_agg_sns_dau_mau` | SNS DAU/MAU | 日 | 日次 |
| `gold_agg_sns_ad_revenue` | SNS広告収益 | 日×枠種別 | 日次 |
| `gold_agg_sns_ad_fill_rate` | 広告枠充填率 | 日×枠種別 | 日次 |
| `gold_agg_si_project_progress` | SI案件進捗 | 契約×週 | 週次 |
| `gold_agg_si_resource_utilization` | SIリソース稼働率 | 種別×月 | 日次 |
| `gold_agg_cross_domain_impact` | クロスドメインインパクト | 顧客×シナリオ | オンデマンド |
| `gold_fact_news_impact_assessments` | ニュースインパクト診断結果 | ニュース×ドメイン | リアルタイム |
| `gold_dim_news_scenarios` | ニュースシナリオマスタ | シナリオ | オンデマンド |

---

## KPI定義（Gold層集計テーブル詳細）

### 全社共通KPI

| KPI名 | テーブル | 計算式 | 閾値 |
|---|---|---|---|
| 統合顧客数 | `gold_agg_revenue_by_domain` | COUNT(DISTINCT unified_customer_id) | — |
| ドメイン横断利用率 | `silver_bridge_domain_mappings` | 2ドメイン以上利用顧客 / 全顧客 | 目標 ≥ 25% |
| 全社月間収益 | `gold_agg_revenue_by_domain` | SUM(monthly_revenue) | — |

### 携帯電話事業KPI

| KPI名 | テーブル | 計算式 | 警告閾値 |
|---|---|---|---|
| ARPU（月間） | `gold_agg_mobile_arpu` | 月間収益 / アクティブ契約数 | < ¥3,500 |
| 解約率（月間） | `gold_agg_churn_risk_scores` | 月間解約数 / 月初契約数 | > 1.5% |
| 在庫充足率 | `gold_agg_mobile_inventory_health` | 在庫数 / 月間予測販売数 | < 1.5ヶ月分 |
| 端末モデル別欠品率 | `gold_agg_mobile_inventory_health` | 欠品モデル数 / 全取扱モデル数 | > 10% |
| MNP純増減 | `silver_fact_transactions_mobile` | MNP転入 - MNP転出 | < 0（連続2ヶ月） |

### SNS事業KPI

| KPI名 | テーブル | 計算式 | 警告閾値 |
|---|---|---|---|
| DAU/MAU比率 | `gold_agg_sns_dau_mau` | DAU / MAU | < 30% |
| 広告収益（日次） | `gold_agg_sns_ad_revenue` | SUM(ad_revenue) | 前週比 -15% |
| 広告枠充填率 | `gold_agg_sns_ad_fill_rate` | 消化IMP / 利用可能IMP | < 70% |
| 有料プラン転換率 | `silver_dim_contracts_sns` | 有料契約数 / 全ユーザー数 | < 5% |
| eCPM | `gold_agg_sns_ad_revenue` | 広告収益 / IMP × 1000 | < ¥200 |

### SI事業KPI

| KPI名 | テーブル | 計算式 | 警告閾値 |
|---|---|---|---|
| パイプライン総額 | `gold_agg_si_project_progress` | SUM(remaining_value) | 前四半期比 -20% |
| リソース稼働率 | `gold_agg_si_resource_utilization` | 稼働中 / (稼働中 + 待機) | < 75% または > 95% |
| 案件遅延率 | `gold_agg_si_project_progress` | 遅延PJ数 / 全PJ数 | > 20% |
| 顧客業種集中度 | `silver_dim_customers_si` | 最大業種比率 | > 40%（特定業種依存リスク） |
| 平均案件粗利率 | `gold_agg_si_project_progress` | (売上 - 原価) / 売上 | < 25% |

### ニュースインパクト分析KPI

| KPI名 | テーブル | 計算式 | 用途 |
|---|---|---|---|
| インパクトスコア | `gold_fact_news_impact_assessments` | スキルロジックにより算出（0-100） | 通知優先度判定 |
| 影響顧客数 | `gold_agg_cross_domain_impact` | ニュース×ドメインで影響を受ける顧客のCOUNT | 規模感把握 |
| 想定損失額 | `gold_fact_news_impact_assessments` | スキルロジックにより推定 | 経営判断材料 |
| 対応緊急度 | `gold_fact_news_impact_assessments` | 施行期限 - 現在日 + リスクレベル | アクション優先順位 |

---

## データフロー概要

```
[ソースDB] ──CDC/差分抽出──> [Bronze] ──品質チェック/型統一──> [Silver] ──集計/KPI計算──> [Gold]
                                                                                         │
                                                                                         ▼
                                                                              [ニュース分析エージェント]
                                                                                         │
                                                                              スキル実行 → インパクト判定
                                                                                         │
                                                                                         ▼
                                                                              [通知配信（Teams/Email/Push）]
```

## 補足: スキルからのデータ参照パターン

各スキル（`.skill.md`）が参照する Fabric テーブルは、主に以下の命名規則に従う:

- `dim_*` → Silver層のディメンションテーブル、またはGold層のマスタ
- `fact_*` → Silver層のファクトテーブル、またはGold層のイベント/トランザクション集計
- `agg_*` → Gold層の事前集計テーブル

スキル内で参照される論理テーブル名（例: `fact_inventory_snapshot`, `dim_terminals`）は、上記メダリオン構成のテーブルに対するセマンティックモデルのビュー名として定義される。
