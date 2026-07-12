# Hosted Agent データマッピング定義 (agent-data-mapping.ds.md)

## 概要

本ドキュメントは、ニュース分析・インパクト診断システムを構成する4つのHosted Agentが使用するデータソースのマッピング、クエリパターン、データフローを定義する。

| 項目 | 値 |
|------|-----|
| エージェント数 | 4 |
| 対象データソース | 5 DB / 18 テーブル |
| Fabric レイクハウス | Gold層を使用 |
| 対象業務領域 | AIエージェント / ゲーム事業 / ネットワークサービス事業 |

---

## エージェント構成

```
[Agent 1: Web情報収集]
        │
        ▼ (記事データ)
[Agent 2: ビジネスインパクト評価]
        │
        ▼ (分析結果)
[Agent 3: 事業部別レコメンド]
        │
        ▼ (通知内容)
[Agent 4: 通知配信]
```

| Agent | 名称 | 役割 | ツール |
|-------|------|------|--------|
| Agent 1 | Web情報収集エージェント | ニュース記事の収集・構造化・格納 | Bing Grounding, Code Interpreter |
| Agent 2 | ビジネスインパクト評価エージェント | KPIデータ参照によるインパクトスコア算出 | Fabric Data Access, Code Interpreter |
| Agent 3 | 事業部別レコメンドエージェント | 各領域の業務データ参照による推奨アクション生成 | Fabric Data Access, Code Interpreter |
| Agent 4 | 通知エージェント | 分析結果を適切なチャネルへ配信 | Microsoft Teams, Email |

---

## Agent 1: Web情報収集エージェント

### データソースマッピング

| 操作 | レイクハウス/テーブル | アクセス種別 |
|------|---------------------|-------------|
| WRITE | lh_news_gold / news_articles | INSERT |
| READ | lh_news_gold / news_articles | SELECT（重複チェック） |

### 外部データソース

| ソース | 用途 | アクセス方式 |
|--------|------|-------------|
| Bing Grounding | ニュース記事の検索・取得 | Azure AI Search API |
| RSS Feed Aggregator | 定時ニュースフィード取得 | HTTP GET |

### 取得クエリパターン

```sql
-- 重複チェック（既存記事との照合）
SELECT article_id, title, published_at
FROM lh_news_gold.news_articles
WHERE published_at >= DATEADD(day, -7, GETDATE())
  AND (title LIKE @title_pattern OR source = @source);

-- 新規記事の格納
INSERT INTO lh_news_gold.news_articles
  (article_id, title, summary, body, source, category, published_at, ingested_at)
VALUES
  (@article_id, @title, @summary, @body, @source, @category, @published_at, GETDATE());
```

### 出力スキーマ

```json
{
  "article_id": "string",
  "title": "string",
  "summary": "string",
  "body": "string",
  "source": "string",
  "category": "regulation | technology | policy_infra | economy | security",
  "published_at": "datetime",
  "extracted_entities": [
    {"type": "string", "value": "string", "confidence": "float"}
  ]
}
```

---

## Agent 2: ビジネスインパクト評価エージェント

### データソースマッピング

| 操作 | レイクハウス/テーブル | アクセス種別 | 用途 |
|------|---------------------|-------------|------|
| READ | lh_news_gold / news_articles | SELECT | 分析対象記事の取得 |
| READ | lh_ai_agent_gold / kpi_monthly | SELECT | AIエージェント事業KPI参照 |
| READ | lh_ai_agent_gold / customers | SELECT | 顧客数・地域分布 |
| READ | lh_ai_agent_gold / contracts | SELECT | 契約状況 |
| READ | lh_ai_agent_gold / transactions | SELECT (集計) | 売上・利用実績 |
| READ | lh_ai_agent_gold / products | SELECT | 製品・GPAIモデル一覧 |
| READ | lh_game_gold / kpi_monthly | SELECT | ゲーム事業KPI参照 |
| READ | lh_game_gold / customers | SELECT | プレイヤー数・地域分布 |
| READ | lh_game_gold / game_titles | SELECT | タイトル・プラットフォーム |
| READ | lh_game_gold / transactions | SELECT (集計) | 売上実績 |
| READ | lh_network_gold / kpi_monthly | SELECT | ネットワーク事業KPI参照 |
| READ | lh_network_gold / customers | SELECT | 顧客数・地域分布 |
| READ | lh_network_gold / contracts | SELECT | 回線契約情報 |
| READ | lh_network_gold / transactions | SELECT (集計) | 売上実績 |
| READ | lh_network_gold / circuits | SELECT | 回線情報 |
| READ | lh_common_gold / unified_customers | SELECT | 統合顧客情報 |
| READ | lh_common_gold / domain_id_mappings | SELECT | ドメイン間マッピング |
| READ | lh_news_gold / impact_analyses | SELECT | 過去の分析結果（トレンド比較） |
| WRITE | lh_news_gold / impact_analyses | INSERT | 分析結果の格納 |

### 取得クエリパターン

```sql
-- 分析対象記事の取得
SELECT article_id, title, summary, body, category, published_at
FROM lh_news_gold.news_articles
WHERE article_id = @target_article_id;

-- AIエージェント事業: EU域内顧客数・売上
SELECT
    COUNT(*) AS eu_customer_count,
    COUNT(*) * 1.0 / (SELECT COUNT(*) FROM lh_ai_agent_gold.customers) AS eu_ratio
FROM lh_ai_agent_gold.customers
WHERE country_code IN ('DE','FR','IT','ES','NL','BE','AT','PL','SE','DK',
                       'FI','IE','PT','GR','CZ','RO','HU','BG','HR','SK',
                       'LT','LV','EE','SI','LU','CY','MT');

-- AIエージェント事業: 年間売上（グローバル/EU）
SELECT
    SUM(amount) AS global_annual_revenue,
    SUM(CASE WHEN region = 'EU' THEN amount ELSE 0 END) AS eu_annual_revenue
FROM lh_ai_agent_gold.transactions
WHERE executed_at >= DATEADD(year, -1, GETDATE())
  AND status = 'completed';

-- AIエージェント事業: GPAIモデル数
SELECT COUNT(*) AS gpai_model_count
FROM lh_ai_agent_gold.products
WHERE category IN ('llm', 'vision', 'speech')
  AND status = 'active';

-- ゲーム事業: EU域内MAU・売上
SELECT
    COUNT(DISTINCT customer_id) AS eu_mau,
    SUM(t.amount) AS eu_annual_revenue
FROM lh_game_gold.customers c
JOIN lh_game_gold.transactions t ON c.customer_id = t.customer_id
WHERE c.country_code IN (/* EU27 */)
  AND t.executed_at >= DATEADD(year, -1, GETDATE());

-- ゲーム事業: タイトル別プラットフォーム分布
SELECT title_id, title_name, platform, status
FROM lh_game_gold.game_titles
WHERE status = 'active';

-- ネットワーク事業: 地域別回線数・顧客数
SELECT
    service_region,
    COUNT(DISTINCT c.customer_id) AS customer_count,
    COUNT(ci.circuit_id) AS circuit_count
FROM lh_network_gold.customers c
LEFT JOIN lh_network_gold.contracts co ON c.customer_id = co.customer_id
LEFT JOIN lh_network_gold.circuits ci ON co.contract_id = ci.contract_id
GROUP BY service_region;

-- ネットワーク事業: 月間KPIトレンド
SELECT year_month, metric_name, metric_value
FROM lh_network_gold.kpi_monthly
WHERE year_month >= DATEADD(month, -6, GETDATE())
ORDER BY year_month;

-- 過去のインパクト分析（トレンド比較）
SELECT domain, impact_level, impact_score, analyzed_at
FROM lh_news_gold.impact_analyses
WHERE domain = @target_domain
  AND analyzed_at >= DATEADD(month, -3, GETDATE())
ORDER BY analyzed_at DESC;

-- 分析結果の格納
INSERT INTO lh_news_gold.impact_analyses
  (analysis_id, article_id, domain, impact_level, impact_score,
   impact_summary, recommended_actions, analyzed_at, model_version)
VALUES
  (@analysis_id, @article_id, @domain, @impact_level, @impact_score,
   @impact_summary, @recommended_actions_json, GETDATE(), @model_version);
```

### 出力スキーマ

```json
{
  "analysis_id": "uuid",
  "article_id": "string",
  "domain": "ai_agent | game | network_service",
  "impact_level": "critical | high | medium | low | none",
  "impact_score": "decimal(5,2)",
  "impact_summary": "string",
  "recommended_actions": [
    {"priority": "P0-P4", "action": "string", "owner": "string", "deadline": "string"}
  ],
  "supporting_data": {
    "affected_customers": "int",
    "revenue_at_risk": "decimal",
    "key_metrics": {}
  }
}
```

---

## Agent 3: 事業部別レコメンドエージェント

### データソースマッピング

| 操作 | レイクハウス/テーブル | アクセス種別 | 用途 |
|------|---------------------|-------------|------|
| READ | lh_news_gold / impact_analyses | SELECT | Agent 2の分析結果取得 |
| READ | lh_news_gold / news_articles | SELECT | 元記事情報参照 |
| READ | lh_ai_agent_gold / customers | SELECT | 影響顧客の特定 |
| READ | lh_ai_agent_gold / contracts | SELECT | 影響契約の特定 |
| READ | lh_ai_agent_gold / products | SELECT | 対象製品の特定 |
| READ | lh_game_gold / customers | SELECT | 影響プレイヤーの特定 |
| READ | lh_game_gold / game_titles | SELECT | 影響タイトルの特定 |
| READ | lh_game_gold / inventory | SELECT | アイテム影響範囲 |
| READ | lh_network_gold / customers | SELECT | 影響顧客の特定 |
| READ | lh_network_gold / contracts | SELECT | 影響契約・SLA情報 |
| READ | lh_network_gold / circuits | SELECT | 影響回線の特定 |
| READ | lh_common_gold / customer_segments | SELECT | 通知対象セグメント特定 |
| READ | lh_common_gold / unified_customers | SELECT | 顧客連絡先情報 |
| WRITE | lh_news_gold / notifications | INSERT | 通知レコード生成 |

### 取得クエリパターン

```sql
-- 分析結果の取得（通知生成対象）
SELECT a.*, n.title AS article_title, n.category
FROM lh_news_gold.impact_analyses a
JOIN lh_news_gold.news_articles n ON a.article_id = n.article_id
WHERE a.analysis_id = @analysis_id;

-- AIエージェント: 影響を受ける顧客・契約の特定
SELECT c.customer_id, c.company_name, c.email, co.contract_id, co.plan_name
FROM lh_ai_agent_gold.customers c
JOIN lh_ai_agent_gold.contracts co ON c.customer_id = co.customer_id
WHERE c.country_code IN (/* 影響対象国 */)
  AND co.status = 'active';

-- ゲーム事業: 影響タイトルとプレイヤー数
SELECT gt.title_id, gt.title_name, gt.platform,
       COUNT(DISTINCT t.customer_id) AS affected_players
FROM lh_game_gold.game_titles gt
JOIN lh_game_gold.transactions t ON gt.title_id = t.title_id
WHERE gt.status = 'active'
  AND gt.platform LIKE @target_platform
GROUP BY gt.title_id, gt.title_name, gt.platform;

-- ネットワーク事業: 影響回線・顧客
SELECT c.customer_id, c.company_name, co.service_type, ci.circuit_id, ci.bandwidth
FROM lh_network_gold.customers c
JOIN lh_network_gold.contracts co ON c.customer_id = co.customer_id
JOIN lh_network_gold.circuits ci ON co.contract_id = ci.contract_id
WHERE c.service_region = @affected_region
  AND ci.status = 'active';

-- 通知対象セグメントの特定
SELECT s.segment_id, s.segment_name, s.domain
FROM lh_common_gold.customer_segments s
WHERE s.domain = @target_domain OR s.domain IS NULL;

-- 通知レコードの生成
INSERT INTO lh_news_gold.notifications
  (notification_id, analysis_id, domain, channel, title, body, priority, status, created_at)
VALUES
  (@notification_id, @analysis_id, @domain, @channel, @title, @body, @priority, 'pending', GETDATE());
```

### 出力スキーマ

```json
{
  "notification_id": "uuid",
  "analysis_id": "uuid",
  "domain": "ai_agent | game | network_service",
  "channel": "teams | email | webhook",
  "priority": "urgent | high | normal | low",
  "title": "string",
  "body": "string (markdown)",
  "recipients": [
    {"role": "string", "name": "string", "contact": "string"}
  ],
  "affected_entities": {
    "customers": ["customer_id"],
    "contracts": ["contract_id"],
    "products_or_titles": ["id"]
  }
}
```

---

## Agent 4: 通知エージェント

### データソースマッピング

| 操作 | レイクハウス/テーブル | アクセス種別 | 用途 |
|------|---------------------|-------------|------|
| READ | lh_news_gold / notifications | SELECT | 送信対象通知の取得 |
| UPDATE | lh_news_gold / notifications | UPDATE | 送信ステータス更新 |

### 外部サービス

| サービス | 用途 | アクセス方式 |
|----------|------|-------------|
| Microsoft Teams | Teams チャネル/個人への通知送信 | Microsoft Graph API |
| Microsoft Outlook | メール送信 | Microsoft Graph API |
| Webhook Endpoint | 外部システム連携通知 | HTTP POST |

### 取得クエリパターン

```sql
-- 送信待ち通知の取得
SELECT notification_id, analysis_id, domain, channel, title, body, priority
FROM lh_news_gold.notifications
WHERE status = 'pending'
ORDER BY
  CASE priority
    WHEN 'urgent' THEN 1
    WHEN 'high' THEN 2
    WHEN 'normal' THEN 3
    WHEN 'low' THEN 4
  END,
  created_at ASC;

-- 送信ステータス更新
UPDATE lh_news_gold.notifications
SET status = @new_status, sent_at = GETDATE()
WHERE notification_id = @notification_id;
```

### 通知チャネル別ルーティング

| priority | channel | 配信方式 | SLA |
|----------|---------|----------|-----|
| urgent | teams | Adaptive Card（即時） | 5分以内 |
| urgent | email | 高優先度メール（即時） | 10分以内 |
| high | teams | Adaptive Card（即時） | 15分以内 |
| normal | teams | 標準メッセージ（バッチ） | 当日中 |
| normal | email | 標準メール（バッチ） | 当日中 |
| low | email | 週次ダイジェスト | 週次 |

---

## データフロー全体図

```
┌─────────────────────────────────────────────────────────────────────┐
│                        External Sources                              │
│  [Bing Grounding]  [RSS Feeds]  [業界ニュースAPI]                    │
└─────────────┬───────────────────────────────────────────────────────┘
              │ (記事取得)
              ▼
┌─────────────────────────┐     ┌─────────────────────────────────┐
│  Agent 1: Web情報収集    │────▶│  lh_news_gold.news_articles     │
│  - Bing Grounding        │     │  (INSERT: 新規記事)              │
│  - 構造化・エンティティ抽出│     └───────────────┬─────────────────┘
└─────────────────────────┘                     │
                                                │ (READ: 分析対象記事)
                                                ▼
┌─────────────────────────┐     ┌─────────────────────────────────┐
│  Agent 2: インパクト評価  │◀───│  Fabric Gold層 (全ドメイン)      │
│  - スキルロジック実行      │     │  - kpi_monthly (各事業部)        │
│  - スコア算出             │     │  - customers / contracts         │
│  - 影響領域判定           │     │  - transactions / products       │
└────────────┬────────────┘     │  - circuits / game_titles        │
             │                   └─────────────────────────────────┘
             │ (WRITE: 分析結果)
             ▼
┌─────────────────────────────────┐
│  lh_news_gold.impact_analyses   │
└───────────────┬─────────────────┘
                │ (READ: 分析結果)
                ▼
┌─────────────────────────┐     ┌─────────────────────────────────┐
│  Agent 3: レコメンド生成  │◀───│  Fabric Gold層 (業務詳細)        │
│  - 事業部別推奨アクション  │     │  - customers (影響顧客特定)      │
│  - 通知内容組み立て       │     │  - contracts (影響契約特定)      │
│  - 対象者選定            │     │  - customer_segments             │
└────────────┬────────────┘     └─────────────────────────────────┘
             │ (WRITE: 通知レコード)
             ▼
┌─────────────────────────────────┐
│  lh_news_gold.notifications     │
└───────────────┬─────────────────┘
                │ (READ: 送信対象)
                ▼
┌─────────────────────────┐     ┌─────────────────────────────────┐
│  Agent 4: 通知配信       │────▶│  External Channels               │
│  - チャネルルーティング    │     │  - Microsoft Teams               │
│  - 送信・ステータス更新   │     │  - Email (Outlook)               │
└─────────────────────────┘     │  - Webhook                       │
                                └─────────────────────────────────┘
```

---

## スキルとデータの対応関係

### AIエージェント事業

| スキル | 使用テーブル (READ) | 主要クエリ |
|--------|--------------------|-----------| 
| regulation-compliance-impact | customers, contracts, transactions, products | EU顧客数・売上・GPAIモデル数 |
| technology-infrastructure-impact | products, contracts, transactions | GPU世代・推論コスト・契約情報 |
| policy-infrastructure-expansion-impact | customers, transactions | 地域別顧客分布・APIコール数 |

### ゲーム事業

| スキル | 使用テーブル (READ) | 主要クエリ |
|--------|--------------------|-----------| 
| regulation-ai-feature-impact | customers, game_titles, transactions, inventory | EU MAU・タイトル別AI機能・売上 |
| technology-gpu-pipeline-impact | customers, game_titles, transactions | プラットフォーム別売上・タイトル技術要件 |
| policy-infrastructure-gaming-expansion | customers, transactions, game_titles | 地域別MAU・オンラインタイトル数 |

### ネットワークサービス事業

| スキル | 使用テーブル (READ) | 主要クエリ |
|--------|--------------------|-----------| 
| regulation-network-ai-impact | customers, contracts, transactions, circuits | EU回線数・SLA顧客数・売上 |
| technology-network-equipment-impact | circuits, contracts, transactions | 機器チップ世代・更改計画・運用コスト |
| policy-fiber-expansion-opportunity | customers, contracts, circuits, transactions | 地域別回線数・顧客数・サービス種別ARPU |

### 共通データ（全スキル共用）

| テーブル | 用途 |
|---------|------|
| lh_common_gold.unified_customers | 顧客の統合属性（地域、種別）参照 |
| lh_common_gold.domain_id_mappings | ドメイン横断での顧客突合 |
| lh_common_gold.customer_segments | 通知対象セグメントの絞り込み |
| lh_news_gold.news_articles | 分析対象記事の取得 |
| lh_news_gold.impact_analyses | 過去分析との比較・トレンド把握 |
| lh_news_gold.notifications | 通知生成・送信管理 |

---

## アクセス権限マトリクス

| Agent | lh_common_gold | lh_ai_agent_gold | lh_game_gold | lh_network_gold | lh_news_gold |
|-------|---------------|-----------------|-------------|----------------|-------------|
| Agent 1 (Web収集) | — | — | — | — | READ/WRITE (articles) |
| Agent 2 (評価) | READ | READ | READ | READ | READ/WRITE (analyses) |
| Agent 3 (レコメンド) | READ | READ | READ | READ | READ/WRITE (notifications) |
| Agent 4 (通知) | — | — | — | — | READ/UPDATE (notifications) |
