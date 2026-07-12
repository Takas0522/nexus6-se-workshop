# Fabric オントロジー定義 (fabric-ontology.ds.md)

## データソース概要

本ドキュメントは、ニュース分析・インパクト診断システムが利用するMicrosoft Fabricデータ基盤のオントロジー（データモデル・関係性・レイヤー構成）を定義する。

| 項目 | 値 |
|------|-----|
| 対象事業領域 | AIエージェント / ゲーム事業 / ネットワークサービス事業 |
| ソースDB数 | 4（common, ai_agent, game, network） + 1（news） |
| エンティティ数 | 18 |
| メダリオン層 | Bronze → Silver → Gold |
| Fabric ワークスペース | ws_nexus6_analytics |

---

## エンティティ定義

### 統合顧客ドメイン（sqldb_common_01）

| エンティティ | テーブル | 説明 | 主キー | 分類 |
|-------------|---------|------|--------|------|
| UnifiedCustomer | unified_customers | 全事業領域の顧客を統合管理するマスタエンティティ。各事業領域の顧客を一意に識別する。 | unified_customer_id | マスタ |
| DomainIdMapping | domain_id_mappings | 統合顧客IDと各事業領域固有の顧客IDを紐付けるブリッジエンティティ。 | mapping_id | ブリッジ |
| CustomerSegment | customer_segments | 顧客セグメント定義。マーケティング施策・通知対象の絞り込みに使用。 | segment_id | マスタ |

### AIエージェント事業ドメイン（sqldb_ai_agent_01）

| エンティティ | テーブル | 説明 | 主キー | 分類 |
|-------------|---------|------|--------|------|
| AIAgentCustomer | customers | AIエージェント事業の顧客。法人・個人を含むサービス利用者。 | customer_id | マスタ |
| AIAgentContract | contracts | APIプラン・サブスクリプション契約。利用上限・料金体系を定義。 | contract_id | マスタ |
| AIAgentTransaction | transactions | APIコール・課金・返金等のトランザクション。利用実績の詳細記録。 | transaction_id | トランザクション |
| AIAgentProduct | products | 提供中のAIモデル・エンドポイント等の製品カタログ。 | product_id | マスタ |

### ゲーム事業ドメイン（sqldb_game_01）

| エンティティ | テーブル | 説明 | 主キー | 分類 |
|-------------|---------|------|--------|------|
| GameCustomer | customers | ゲーム事業のプレイヤー・顧客。プラットフォーム・地域情報を保持。 | customer_id | マスタ |
| GameTitle | game_titles | ゲームタイトルマスタ。ジャンル・プラットフォーム・リリース情報。 | title_id | マスタ |
| GameTransaction | transactions | アイテム購入・ガチャ・サブスク等の課金トランザクション。 | transaction_id | トランザクション |
| GameInventory | inventory | プレイヤーが所持する仮想アイテムの在庫。有効期限管理を含む。 | inventory_id | 状態 |

### ネットワークサービス事業ドメイン（sqldb_network_01）

| エンティティ | テーブル | 説明 | 主キー | 分類 |
|-------------|---------|------|--------|------|
| NetworkCustomer | customers | ネットワークサービスの法人・個人顧客。サービス地域・種別を保持。 | customer_id | マスタ |
| NetworkContract | contracts | 回線・VPN・クラウド接続等のサービス契約。帯域・SLA情報を含む。 | contract_id | マスタ |
| NetworkTransaction | transactions | 月額課金・超過料金・セットアップ費等の課金トランザクション。 | transaction_id | トランザクション |
| NetworkCircuit | circuits | 物理/論理回線・接続ポイント。端点間のネットワーク構成情報。 | circuit_id | 状態 |

### ニュース分析ドメイン（sqldb_news_01）

| エンティティ | テーブル | 説明 | 主キー | 分類 |
|-------------|---------|------|--------|------|
| NewsArticle | news_articles | 取り込んだニュース記事。配信元・カテゴリ・公開日時を管理。 | article_id | イベント |
| ImpactAnalysis | impact_analyses | AIエージェントが生成したインパクト分析結果。スコア・推奨アクション。 | analysis_id | イベント |
| Notification | notifications | 業務部門への通知送信履歴。チャネル・送信状況を追跡。 | notification_id | イベント |

---

## リレーション定義

### ER図（テキスト表現）

```
UnifiedCustomer ──1:N──> DomainIdMapping
DomainIdMapping ──N:1──> AIAgentCustomer     [domain='ai_agent']
DomainIdMapping ──N:1──> GameCustomer         [domain='game']
DomainIdMapping ──N:1──> NetworkCustomer      [domain='network_service']

AIAgentCustomer ──1:N──> AIAgentContract
AIAgentContract ──1:N──> AIAgentTransaction
AIAgentProduct  ──1:N──> AIAgentTransaction   [via product_id/api_endpoint]

GameCustomer    ──1:N──> GameTransaction
GameCustomer    ──1:N──> GameInventory
GameTitle       ──1:N──> GameTransaction
GameTitle       ──1:N──> GameInventory

NetworkCustomer ──1:N──> NetworkContract
NetworkContract ──1:N──> NetworkTransaction
NetworkContract ──1:N──> NetworkCircuit

NewsArticle     ──1:N──> ImpactAnalysis
ImpactAnalysis  ──1:N──> Notification

CustomerSegment ──M:N──> UnifiedCustomer      [via criteria_json matching]
```

### リレーション詳細

| 親エンティティ | 子エンティティ | カーディナリティ | 結合キー | 説明 |
|---------------|---------------|-----------------|----------|------|
| UnifiedCustomer | DomainIdMapping | 1:N | unified_customer_id | 1顧客は複数ドメインにマッピング可能 |
| DomainIdMapping | AIAgentCustomer | N:1 | domain_customer_id = customer_id | domain='ai_agent'でフィルタ |
| DomainIdMapping | GameCustomer | N:1 | domain_customer_id = customer_id | domain='game'でフィルタ |
| DomainIdMapping | NetworkCustomer | N:1 | domain_customer_id = customer_id | domain='network_service'でフィルタ |
| AIAgentCustomer | AIAgentContract | 1:N | customer_id | 1顧客は複数契約保持可能 |
| AIAgentContract | AIAgentTransaction | 1:N | contract_id | 1契約に複数トランザクション |
| GameCustomer | GameTransaction | 1:N | customer_id | 1プレイヤーは複数決済 |
| GameCustomer | GameInventory | 1:N | customer_id | 1プレイヤーは複数アイテム保持 |
| GameTitle | GameTransaction | 1:N | title_id | 1タイトルに複数課金 |
| GameTitle | GameInventory | 1:N | title_id | 1タイトルに複数アイテム存在 |
| NetworkCustomer | NetworkContract | 1:N | customer_id | 1顧客は複数回線契約可能 |
| NetworkContract | NetworkTransaction | 1:N | contract_id | 1契約に複数課金 |
| NetworkContract | NetworkCircuit | 1:N | contract_id | 1契約に複数回線 |
| NewsArticle | ImpactAnalysis | 1:N | article_id | 1記事に対し複数領域の分析 |
| ImpactAnalysis | Notification | 1:N | analysis_id | 1分析結果から複数通知生成 |

---

## メダリオン構成

### Bronze 層（Raw Ingestion）

ソースDBからの生データ取り込み。スキーマ変換なし、追記のみ。

| レイクハウス | テーブル | ソース | 取り込み頻度 |
|-------------|---------|--------|-------------|
| lh_common_bronze | raw_unified_customers | sqldb_common_01.unified_customers | 日次 |
| lh_common_bronze | raw_domain_id_mappings | sqldb_common_01.domain_id_mappings | 日次 |
| lh_common_bronze | raw_customer_segments | sqldb_common_01.customer_segments | 日次 |
| lh_ai_agent_bronze | raw_customers | sqldb_ai_agent_01.customers | 日次 |
| lh_ai_agent_bronze | raw_contracts | sqldb_ai_agent_01.contracts | 日次 |
| lh_ai_agent_bronze | raw_transactions | sqldb_ai_agent_01.transactions | 時間次 |
| lh_ai_agent_bronze | raw_products | sqldb_ai_agent_01.products | 日次 |
| lh_game_bronze | raw_customers | sqldb_game_01.customers | 日次 |
| lh_game_bronze | raw_game_titles | sqldb_game_01.game_titles | 日次 |
| lh_game_bronze | raw_transactions | sqldb_game_01.transactions | 時間次 |
| lh_game_bronze | raw_inventory | sqldb_game_01.inventory | 日次 |
| lh_network_bronze | raw_customers | sqldb_network_01.customers | 日次 |
| lh_network_bronze | raw_contracts | sqldb_network_01.contracts | 日次 |
| lh_network_bronze | raw_transactions | sqldb_network_01.transactions | 時間次 |
| lh_network_bronze | raw_circuits | sqldb_network_01.circuits | 日次 |
| lh_news_bronze | raw_news_articles | sqldb_news_01.news_articles | リアルタイム |
| lh_news_bronze | raw_impact_analyses | sqldb_news_01.impact_analyses | リアルタイム |
| lh_news_bronze | raw_notifications | sqldb_news_01.notifications | リアルタイム |

### Silver 層（Cleansed & Conformed）

データクレンジング、型変換、重複排除、SCD Type 2 適用。

| レイクハウス | テーブル | 変換内容 |
|-------------|---------|----------|
| lh_common_silver | customers_cleansed | NULL補完、メール正規化、ステータス統一 |
| lh_common_silver | domain_mappings_validated | 参照整合性チェック済みマッピング |
| lh_common_silver | segments_current | 有効セグメントのみ抽出 |
| lh_ai_agent_silver | customers_cleansed | 業種コード正規化、地域コード標準化 |
| lh_ai_agent_silver | contracts_active | 有効契約のみ、金額通貨統一（JPY） |
| lh_ai_agent_silver | transactions_validated | 重複排除、ステータス補正、タイムゾーン統一 |
| lh_ai_agent_silver | products_current | 有効製品のみ、カテゴリ正規化 |
| lh_game_silver | customers_cleansed | 年齢層補完、プラットフォーム正規化 |
| lh_game_silver | game_titles_current | 運営中タイトル、レーティング正規化 |
| lh_game_silver | transactions_validated | 重複排除、通貨統一、プラットフォーム正規化 |
| lh_game_silver | inventory_current | 有効期限内アイテムのみ |
| lh_network_silver | customers_cleansed | サービス地域コード標準化 |
| lh_network_silver | contracts_active | 有効契約、帯域幅正規化、通貨統一 |
| lh_network_silver | transactions_validated | 重複排除、請求期間正規化 |
| lh_network_silver | circuits_active | 稼働中回線のみ、端点情報正規化 |
| lh_news_silver | articles_enriched | NER抽出結果付与、カテゴリ正規化 |
| lh_news_silver | analyses_validated | スコア範囲チェック、モデルバージョン整合 |
| lh_news_silver | notifications_tracked | 送信ステータス最新化 |

### Gold 層（Business-Ready / Aggregated）

ビジネスユーザー・AIエージェントが直接利用する集計・統合ビュー。

| レイクハウス | テーブル | 説明 |
|-------------|---------|------|
| lh_common_gold | unified_customers | 統合顧客マスタ（全ドメイン属性統合済み） |
| lh_common_gold | domain_id_mappings | 検証済みドメインマッピング |
| lh_common_gold | customer_segments | 有効セグメント定義 |
| lh_common_gold | customer_360 | 顧客360°ビュー（全ドメインの契約・取引サマリ統合） |
| lh_ai_agent_gold | customers | AIエージェント顧客マスタ |
| lh_ai_agent_gold | contracts | 有効契約一覧 |
| lh_ai_agent_gold | transactions | 検証済みトランザクション |
| lh_ai_agent_gold | products | 製品カタログ |
| lh_ai_agent_gold | kpi_monthly | 月次KPI集計テーブル |
| lh_ai_agent_gold | customer_revenue_summary | 顧客別売上サマリ |
| lh_game_gold | customers | ゲーム顧客マスタ |
| lh_game_gold | game_titles | タイトルマスタ |
| lh_game_gold | transactions | 検証済みトランザクション |
| lh_game_gold | inventory | 有効アイテム在庫 |
| lh_game_gold | kpi_monthly | 月次KPI集計テーブル |
| lh_game_gold | title_revenue_summary | タイトル別売上サマリ |
| lh_network_gold | customers | ネットワーク顧客マスタ |
| lh_network_gold | contracts | 有効契約一覧 |
| lh_network_gold | transactions | 検証済みトランザクション |
| lh_network_gold | circuits | 稼働中回線一覧 |
| lh_network_gold | kpi_monthly | 月次KPI集計テーブル |
| lh_network_gold | region_revenue_summary | 地域別売上サマリ |
| lh_news_gold | news_articles | エンリッチ済みニュース記事 |
| lh_news_gold | impact_analyses | インパクト分析結果 |
| lh_news_gold | notifications | 通知履歴 |
| lh_news_gold | impact_trend_summary | インパクトトレンド集計 |

---

## KPI定義（Gold層 集計テーブル）

### lh_ai_agent_gold.kpi_monthly

| KPI | 算出ロジック | 単位 |
|-----|-------------|------|
| monthly_revenue | SUM(transactions.amount) WHERE status='completed' | 百万円 |
| operating_margin | (revenue - cost) / revenue * 100 | % |
| active_customers | COUNT(DISTINCT customer_id) WHERE has_transaction_in_month | 千人 |
| arpu | monthly_revenue / active_customers | 円 |
| churn_rate | lost_customers / previous_month_customers * 100 | % |
| nps | survey_score aggregation | ポイント |
| api_uptime | successful_minutes / total_minutes * 100 | % |
| infra_cost_ratio | infra_cost / monthly_revenue * 100 | % |

### lh_game_gold.kpi_monthly

| KPI | 算出ロジック | 単位 |
|-----|-------------|------|
| monthly_revenue | SUM(transactions.amount) WHERE status='completed' | 百万円 |
| operating_margin | (revenue - cost) / revenue * 100 | % |
| mau | COUNT(DISTINCT customer_id) WHERE has_activity_in_month | 千人 |
| arpu | monthly_revenue / mau | 円 |
| paying_ratio | paying_users / mau * 100 | % |
| dau_mau_ratio | avg_daily_active / mau * 100 | % |
| new_title_releases | COUNT(game_titles) WHERE release_date IN month | 件 |
| infra_cost_ratio | server_cost / monthly_revenue * 100 | % |

### lh_network_gold.kpi_monthly

| KPI | 算出ロジック | 単位 |
|-----|-------------|------|
| monthly_revenue | SUM(transactions.amount) WHERE status='completed' | 百万円 |
| operating_margin | (revenue - cost) / revenue * 100 | % |
| active_circuits | COUNT(circuits) WHERE status='active' | 千回線 |
| arpu | monthly_revenue / active_customers | 円 |
| churn_rate | lost_customers / previous_month_customers * 100 | % |
| network_availability | uptime_minutes / total_minutes * 100 | % |
| incident_count | COUNT(incidents) WHERE severity IN ('P1','P2') | 件 |
| capex_monthly | SUM(capital_expenditure) | 百万円 |

### lh_news_gold.impact_trend_summary

| KPI | 算出ロジック | 単位 |
|-----|-------------|------|
| articles_ingested | COUNT(news_articles) WHERE ingested_in_period | 件 |
| analyses_generated | COUNT(impact_analyses) WHERE analyzed_in_period | 件 |
| avg_impact_score_by_domain | AVG(impact_score) GROUP BY domain | スコア |
| critical_high_count | COUNT(*) WHERE impact_level IN ('critical','high') | 件 |
| notifications_sent | COUNT(notifications) WHERE status='sent' | 件 |
| avg_response_time | AVG(notification.sent_at - article.published_at) | 分 |
