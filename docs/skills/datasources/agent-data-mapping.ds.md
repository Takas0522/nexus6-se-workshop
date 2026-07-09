# Hosted Agent データマッピング定義

## エージェント構成概要

ニュース分析・インパクト診断システムは4つのエージェントがパイプラインとして連携動作する。各エージェントは特定のデータソースへのアクセス権限を持ち、前段の出力を後段が入力として受け取る。

```
[Agent 1: Web情報収集] ──ニュース構造化データ──> [Agent 2: インパクト評価]
                                                        │
                                              インパクトスコア+影響領域
                                                        │
                                                        ▼
                                             [Agent 3: 事業部別レコメンド]
                                                        │
                                              レコメンド+対象者リスト
                                                        │
                                                        ▼
                                             [Agent 4: 通知配信]
```

### エージェント一覧

| Agent ID | 名称 | 役割 | ツール/接続 |
|---|---|---|---|
| `agent-web-collector` | Web情報収集エージェント | ニュースのクロール・構造化・カテゴリ分類 | Bing Grounding, Web検索API |
| `agent-impact-evaluator` | ビジネスインパクト評価エージェント | KPIデータとニュースの照合によるスコアリング | Fabric SQL Endpoint, スキル実行エンジン |
| `agent-domain-recommender` | 事業部別レコメンドエージェント | 各業務領域固有のアクション策定・対象者特定 | Fabric SQL Endpoint, 各ドメインDB |
| `agent-notifier` | 通知エージェント | レコメンド内容のフォーマットと配信 | Microsoft Teams, Outlook, Power Automate |

---

## Agent 1: Web情報収集エージェント

### データソースマッピング

| データソース | アクセス種別 | 用途 |
|---|---|---|
| Bing Grounding API | READ | ニュース記事の検索・取得 |
| `gold_dim_news_scenarios` | READ | 既知シナリオとの照合 |
| `gold_fact_news_impact_assessments` | WRITE | 新規ニュースの登録 |

### 入力

| パラメータ | ソース | 説明 |
|---|---|---|
| `search_keywords` | 設定ファイル | 業務領域に関連するキーワードリスト |
| `monitoring_categories` | 設定ファイル | 監視対象カテゴリ（サプライチェーン、規制、インフラ等） |
| `last_crawl_timestamp` | システム状態 | 前回クロール時刻 |

### 出力スキーマ

```json
{
  "news_id": "uuid",
  "title": "ニュースタイトル",
  "summary": "要約（500文字以内）",
  "source_url": "https://...",
  "published_at": "2026-07-09T00:00:00Z",
  "category": "supply_chain | regulation | infrastructure | competition | economy",
  "keywords": ["半導体", "供給不足", "端末"],
  "estimated_domains": ["mobile", "si"],
  "confidence_score": 0.85,
  "raw_content": "記事全文"
}
```

### クエリパターン

```sql
-- 既知シナリオとのマッチング確認
SELECT scenario_id, title, category, impact_domains
FROM gold_dim_news_scenarios
WHERE category = @detected_category
  AND is_active = 1;

-- 新規ニュースの登録
INSERT INTO gold_fact_news_impact_assessments (
    news_id, title, category, source_url, published_at,
    estimated_domains, status, created_at
) VALUES (
    @news_id, @title, @category, @source_url, @published_at,
    @estimated_domains, 'pending_evaluation', GETUTCDATE()
);
```

---

## Agent 2: ビジネスインパクト評価エージェント

### データソースマッピング

| データソース | アクセス種別 | 用途 |
|---|---|---|
| `gold_fact_news_impact_assessments` | READ/WRITE | ニュース情報の取得・スコア書き込み |
| `gold_agg_revenue_by_domain` | READ | ドメイン別収益でインパクト規模推定 |
| `gold_agg_mobile_inventory_health` | READ | 在庫健全性（半導体シナリオ） |
| `gold_agg_mobile_arpu` | READ | ARPU推移（解約リスク算出） |
| `gold_agg_churn_risk_scores` | READ | 解約リスクスコア |
| `gold_agg_sns_ad_revenue` | READ | 広告収益トレンド（DSAシナリオ） |
| `gold_agg_sns_ad_fill_rate` | READ | 広告充填率 |
| `gold_agg_si_project_progress` | READ | SI案件進捗（納期リスク） |
| `gold_agg_si_resource_utilization` | READ | リソース稼働率 |
| `gold_agg_cross_domain_impact` | WRITE | クロスドメイン影響結果書き込み |

### スキル呼び出しマッピング

| ニュースカテゴリ | 影響ドメイン | 実行スキル |
|---|---|---|
| `supply_chain` | mobile | `mobile/supplychain-semiconductor-shortage.skill.md` |
| `supply_chain` | sns | `sns/supplychain-semiconductor-shortage.skill.md` |
| `supply_chain` | si | `si/supplychain-semiconductor-shortage.skill.md` |
| `regulation` | mobile | `mobile/regulation-data-privacy-dsa.skill.md` |
| `regulation` | sns | `sns/regulation-data-privacy-dsa.skill.md` |
| `regulation` | si | `si/regulation-data-privacy-dsa.skill.md` |
| `infrastructure` | mobile | `mobile/infrastructure-telecom-quality-standard.skill.md` |
| `infrastructure` | sns | `sns/infrastructure-telecom-quality-standard.skill.md` |
| `infrastructure` | si | `si/infrastructure-telecom-quality-standard.skill.md` |

### クエリパターン

```sql
-- 評価対象ニュースの取得
SELECT news_id, title, category, estimated_domains, raw_content
FROM gold_fact_news_impact_assessments
WHERE status = 'pending_evaluation'
ORDER BY published_at DESC;

-- 携帯事業: 在庫健全性の取得（半導体シナリオ）
SELECT device_model, quantity, days_of_stock, reorder_point,
       stockout_risk_level, last_replenishment_date
FROM gold_agg_mobile_inventory_health
WHERE snapshot_date = CAST(GETUTCDATE() AS DATE);

-- SNS事業: 広告収益トレンド（DSAシナリオ）
SELECT report_date, slot_type, revenue_jpy, impressions, ecpm,
       fill_rate, revenue_change_wow_pct
FROM gold_agg_sns_ad_revenue
WHERE report_date >= DATEADD(month, -3, GETUTCDATE())
ORDER BY report_date DESC;

-- SI事業: リソース稼働率（通信品質シナリオ）
SELECT resource_type, skill_area, available_count, utilized_count,
       utilization_rate, avg_project_duration_months
FROM gold_agg_si_resource_utilization
WHERE month = FORMAT(GETUTCDATE(), 'yyyy-MM');

-- インパクトスコアの書き込み
UPDATE gold_fact_news_impact_assessments
SET impact_score = @score,
    impact_level = @level,
    affected_domains = @domains,
    estimated_financial_impact_jpy = @financial_impact,
    status = 'evaluated',
    evaluated_at = GETUTCDATE()
WHERE news_id = @news_id;

-- クロスドメインインパクトの書き込み
INSERT INTO gold_agg_cross_domain_impact (
    news_id, unified_customer_id, domain, impact_type,
    estimated_impact_jpy, created_at
)
SELECT @news_id, uc.unified_customer_id, dm.domain,
       @impact_type, @estimated_impact_per_customer, GETUTCDATE()
FROM silver_dim_customers_unified uc
JOIN silver_bridge_domain_mappings dm ON uc.unified_customer_id = dm.unified_customer_id
WHERE dm.domain IN (@affected_domains)
  AND uc.segment_code IN (@target_segments);
```

---

## Agent 3: 事業部別レコメンドエージェント

### データソースマッピング

| データソース | アクセス種別 | 用途 |
|---|---|---|
| `gold_fact_news_impact_assessments` | READ | 評価済みインパクト情報 |
| `gold_agg_cross_domain_impact` | READ | 影響を受ける顧客リスト |
| `sqldb_common_01.unified_customers` | READ | 顧客基本情報 |
| `sqldb_common_01.domain_id_mappings` | READ | ドメイン間ID解決 |
| `sqldb_common_01.customer_segments` | READ | セグメント情報（通知優先度） |
| `sqldb_mobile_01.customers` | READ | 携帯顧客詳細 |
| `sqldb_mobile_01.contracts` | READ | 契約状況（更新期・割賦残） |
| `sqldb_mobile_01.transactions` | READ | 直近取引（アクティブ度判定） |
| `sqldb_mobile_01.inventory` | READ | 在庫状況（代替提案） |
| `sqldb_sns_01.customers` | READ | SNSユーザー詳細 |
| `sqldb_sns_01.contracts` | READ | 有料プラン契約状況 |
| `sqldb_sns_01.transactions` | READ | 広告/課金トレンド |
| `sqldb_sns_01.ad_inventory` | READ | 広告枠状況 |
| `sqldb_si_01.customers` | READ | SI顧客（業種） |
| `sqldb_si_01.contracts` | READ | 案件契約（影響PJ特定） |
| `sqldb_si_01.transactions` | READ | 取引状況 |
| `sqldb_si_01.inventory` | READ | リソース在庫 |

### ドメイン別レコメンドロジック

#### 携帯電話事業

```sql
-- 影響を受ける顧客の特定（契約更新期 + 在庫不足端末利用者）
SELECT mc.customer_id, mc.plan_type, mc.status,
       mcon.plan_code, mcon.end_date, mcon.monthly_fee,
       cs.segment_code, cs.segment_name
FROM sqldb_mobile_01.customers mc
JOIN sqldb_common_01.domain_id_mappings dm
    ON mc.customer_id = dm.domain_customer_id AND dm.domain = 'mobile'
JOIN sqldb_common_01.customer_segments cs
    ON dm.unified_customer_id = cs.unified_customer_id
LEFT JOIN sqldb_mobile_01.contracts mcon
    ON mc.customer_id = mcon.customer_id
WHERE mc.status = 'active'
  AND mcon.end_date BETWEEN GETUTCDATE() AND DATEADD(month, 3, GETUTCDATE())
ORDER BY cs.segment_code, mcon.end_date;

-- 代替端末の在庫確認
SELECT device_model, SUM(quantity) AS total_stock, warehouse_code
FROM sqldb_mobile_01.inventory
WHERE quantity > 0
GROUP BY device_model, warehouse_code
ORDER BY total_stock DESC;
```

#### SNS事業

```sql
-- 広告収益への影響を受けるアカウント（ビジネスアカウント＋高額出稿者）
SELECT sc.customer_id, sc.username, sc.account_tier,
       SUM(st.amount) AS recent_ad_spend,
       COUNT(st.transaction_id) AS transaction_count
FROM sqldb_sns_01.customers sc
JOIN sqldb_sns_01.transactions st ON sc.customer_id = st.customer_id
WHERE sc.account_tier IN ('business', 'premium')
  AND st.transaction_type = 'ad_revenue'
  AND st.transaction_date >= DATEADD(month, -1, GETUTCDATE())
GROUP BY sc.customer_id, sc.username, sc.account_tier
HAVING SUM(st.amount) > 100000
ORDER BY recent_ad_spend DESC;

-- 広告枠の影響分析
SELECT slot_type, SUM(available_impressions) AS total_impressions,
       AVG(unit_price) AS avg_cpm
FROM sqldb_sns_01.ad_inventory
GROUP BY slot_type;
```

#### SI事業

```sql
-- 影響を受けるプロジェクトの特定（通信事業者顧客 + インフラ案件）
SELECT sic.customer_id, sic.company_name, sic.industry,
       con.contract_id, con.project_name, con.contract_value,
       con.start_date, con.end_date
FROM sqldb_si_01.customers sic
JOIN sqldb_si_01.contracts con ON sic.customer_id = con.customer_id
WHERE sic.industry IN ('通信', '情報通信', 'ITインフラ')
  AND con.end_date > GETUTCDATE()
ORDER BY con.contract_value DESC;

-- リソース充足度の確認
SELECT resource_type, resource_name, available_count
FROM sqldb_si_01.inventory
WHERE resource_type IN ('engineer', 'hardware')
ORDER BY resource_type, available_count;
```

### 出力スキーマ

```json
{
  "news_id": "uuid",
  "domain": "mobile | sns | si",
  "impact_level": "critical | high | medium | low",
  "recommendations": [
    {
      "action_id": "uuid",
      "priority": 1,
      "title": "アクションタイトル",
      "description": "詳細説明",
      "target_department": "部門名",
      "deadline_suggestion": "2026-07-16",
      "estimated_effort": "high | medium | low"
    }
  ],
  "affected_customers": {
    "count": 1500,
    "segments": {"vip": 200, "standard": 1000, "basic": 300},
    "top_risk_customers": ["customer_id_1", "customer_id_2"]
  },
  "notification_targets": [
    {
      "role": "事業部長",
      "channel": "teams_direct",
      "urgency": "immediate"
    },
    {
      "role": "営業担当",
      "channel": "teams_channel",
      "urgency": "within_24h"
    }
  ]
}
```

---

## Agent 4: 通知エージェント

### データソースマッピング

| データソース | アクセス種別 | 用途 |
|---|---|---|
| Agent 3 出力 | READ | レコメンド内容・対象者リスト |
| Microsoft Teams API | WRITE | チャネル/DM通知送信 |
| Microsoft Outlook API | WRITE | メール通知送信 |
| Power Automate | TRIGGER | ワークフロー起動 |
| `gold_fact_news_impact_assessments` | WRITE | 通知ステータス更新 |

### 通知チャネル選択ロジック

| インパクトレベル | 対象ロール | チャネル | タイミング |
|---|---|---|---|
| critical | 経営層 + 事業部長 | Teams DM + Email | 即座 |
| critical | 部門マネージャー | Teams チャネル | 即座 |
| high | 事業部長 | Teams チャネル | 1時間以内 |
| high | 担当者 | Teams チャネル | 4時間以内 |
| medium | 部門マネージャー | Teams チャネル | 24時間以内 |
| medium | 担当者 | Email（日次ダイジェスト） | 翌営業日朝 |
| low | 関連者 | Email（週次レポート） | 週次 |

### Teams Adaptive Card テンプレート

```json
{
  "type": "AdaptiveCard",
  "version": "1.5",
  "body": [
    {
      "type": "Container",
      "style": "attention",
      "items": [
        {"type": "TextBlock", "text": "⚠️ ビジネスインパクト通知", "weight": "bolder", "size": "large"},
        {"type": "TextBlock", "text": "${news_title}", "weight": "bolder"},
        {"type": "FactSet", "facts": [
          {"title": "カテゴリ", "value": "${category}"},
          {"title": "影響領域", "value": "${affected_domains}"},
          {"title": "インパクトスコア", "value": "${impact_score}/100 (${impact_level})"},
          {"title": "影響顧客数", "value": "${affected_customer_count}名"},
          {"title": "想定影響額", "value": "¥${estimated_financial_impact}"}
        ]}
      ]
    },
    {
      "type": "Container",
      "items": [
        {"type": "TextBlock", "text": "推奨アクション", "weight": "bolder"},
        {"type": "TextBlock", "text": "${recommendations_summary}", "wrap": true}
      ]
    }
  ],
  "actions": [
    {"type": "Action.OpenUrl", "title": "詳細を確認", "url": "${detail_url}"},
    {"type": "Action.Submit", "title": "対応開始", "data": {"action": "acknowledge", "news_id": "${news_id}"}}
  ]
}
```

### クエリパターン

```sql
-- 通知ステータスの更新
UPDATE gold_fact_news_impact_assessments
SET notification_status = 'sent',
    notified_at = GETUTCDATE(),
    notification_channels = @channels,
    notification_recipients_count = @recipient_count
WHERE news_id = @news_id;
```

---

## データフロー詳細

### 全体フロー（時系列）

```
T+0min   [Agent 1] Bing Grounding でニュース検出
           ├─ 読取: Bing API, gold_dim_news_scenarios
           └─ 書込: gold_fact_news_impact_assessments (status='pending_evaluation')

T+1min   [Agent 2] インパクト評価実行
           ├─ 読取: gold_fact_news_impact_assessments, gold_agg_* (各種KPI)
           ├─ 実行: 該当スキル（カテゴリ×ドメインで最大3スキル並列）
           ├─ 書込: gold_fact_news_impact_assessments (status='evaluated', score付与)
           └─ 書込: gold_agg_cross_domain_impact (顧客別影響)

T+3min   [Agent 3] 事業部別レコメンド策定
           ├─ 読取: gold_fact_news_impact_assessments, gold_agg_cross_domain_impact
           ├─ 読取: sqldb_common_01.* (顧客・セグメント)
           ├─ 読取: sqldb_mobile_01.* / sqldb_sns_01.* / sqldb_si_01.* (業務データ)
           └─ 出力: レコメンドJSON（アクション・対象者・通知先）

T+5min   [Agent 4] 通知配信
           ├─ 読取: Agent 3 出力
           ├─ 書込: Teams API (Adaptive Card送信)
           ├─ 書込: Outlook API (メール送信)
           └─ 書込: gold_fact_news_impact_assessments (notification_status='sent')
```

### データアクセス権限マトリクス

| テーブル/リソース | Agent 1 | Agent 2 | Agent 3 | Agent 4 |
|---|---|---|---|---|
| Bing Grounding API | R | - | - | - |
| `gold_dim_news_scenarios` | R | R | - | - |
| `gold_fact_news_impact_assessments` | W | R/W | R | W |
| `gold_agg_revenue_by_domain` | - | R | - | - |
| `gold_agg_mobile_inventory_health` | - | R | - | - |
| `gold_agg_mobile_arpu` | - | R | - | - |
| `gold_agg_churn_risk_scores` | - | R | - | - |
| `gold_agg_sns_ad_revenue` | - | R | - | - |
| `gold_agg_sns_ad_fill_rate` | - | R | - | - |
| `gold_agg_si_project_progress` | - | R | - | - |
| `gold_agg_si_resource_utilization` | - | R | - | - |
| `gold_agg_cross_domain_impact` | - | W | R | - |
| `sqldb_common_01.unified_customers` | - | - | R | - |
| `sqldb_common_01.domain_id_mappings` | - | - | R | - |
| `sqldb_common_01.customer_segments` | - | - | R | - |
| `sqldb_mobile_01.customers` | - | - | R | - |
| `sqldb_mobile_01.contracts` | - | - | R | - |
| `sqldb_mobile_01.transactions` | - | - | R | - |
| `sqldb_mobile_01.inventory` | - | - | R | - |
| `sqldb_sns_01.customers` | - | - | R | - |
| `sqldb_sns_01.contracts` | - | - | R | - |
| `sqldb_sns_01.transactions` | - | - | R | - |
| `sqldb_sns_01.ad_inventory` | - | - | R | - |
| `sqldb_si_01.customers` | - | - | R | - |
| `sqldb_si_01.contracts` | - | - | R | - |
| `sqldb_si_01.transactions` | - | - | R | - |
| `sqldb_si_01.inventory` | - | - | R | - |
| Microsoft Teams API | - | - | - | W |
| Microsoft Outlook API | - | - | - | W |
| Power Automate | - | - | - | T |

> R=Read, W=Write, R/W=Read+Write, T=Trigger

---

## スキルとデータの対応関係

### 携帯電話事業スキル

| スキルファイル | 主要参照テーブル | 主要KPI |
|---|---|---|
| `supplychain-semiconductor-shortage.skill.md` | `gold_agg_mobile_inventory_health`, `sqldb_mobile_01.inventory`, `sqldb_mobile_01.contracts` | 在庫充足率、欠品率、MNP純増減 |
| `regulation-data-privacy-dsa.skill.md` | `gold_agg_mobile_arpu`, `sqldb_mobile_01.transactions`, `sqldb_mobile_01.customers` | ARPU、顧客データ利用率 |
| `infrastructure-telecom-quality-standard.skill.md` | `gold_agg_churn_risk_scores`, `sqldb_mobile_01.contracts`, `gold_agg_revenue_by_domain` | 解約率、設備投資比率 |

### SNS事業スキル

| スキルファイル | 主要参照テーブル | 主要KPI |
|---|---|---|
| `supplychain-semiconductor-shortage.skill.md` | `gold_agg_sns_dau_mau`, `sqldb_sns_01.customers`, `sqldb_sns_01.transactions` | DAU/MAU比率、新規登録数 |
| `regulation-data-privacy-dsa.skill.md` | `gold_agg_sns_ad_revenue`, `gold_agg_sns_ad_fill_rate`, `sqldb_sns_01.ad_inventory` | eCPM、充填率、広告収益 |
| `infrastructure-telecom-quality-standard.skill.md` | `gold_agg_sns_dau_mau`, `gold_agg_sns_ad_revenue`, `sqldb_sns_01.transactions` | DAU/MAU、広告収益、可用性 |

### SI事業スキル

| スキルファイル | 主要参照テーブル | 主要KPI |
|---|---|---|
| `supplychain-semiconductor-shortage.skill.md` | `gold_agg_si_project_progress`, `sqldb_si_01.contracts`, `sqldb_si_01.inventory` | 案件遅延率、HW在庫 |
| `regulation-data-privacy-dsa.skill.md` | `gold_agg_si_project_progress`, `gold_agg_si_resource_utilization`, `sqldb_si_01.customers` | パイプライン総額、リソース稼働率 |
| `infrastructure-telecom-quality-standard.skill.md` | `gold_agg_si_resource_utilization`, `sqldb_si_01.contracts`, `sqldb_si_01.inventory` | リソース稼働率、通信セクター比率 |

---

## 接続・認証設定

| 接続先 | 認証方式 | 備考 |
|---|---|---|
| Fabric SQL Endpoint | Managed Identity (Entra ID) | Lakehouse/Warehouse 経由 |
| sqldb_common_01 | Managed Identity | Azure SQL Database |
| sqldb_mobile_01 | Managed Identity | Azure SQL Database |
| sqldb_sns_01 | Managed Identity | Azure SQL Database |
| sqldb_si_01 | Managed Identity | Azure SQL Database |
| Bing Grounding | API Key (Key Vault) | AI Foundry 組み込み |
| Microsoft Teams | Managed Identity (Graph API) | アプリ権限: ChannelMessage.Send |
| Microsoft Outlook | Managed Identity (Graph API) | アプリ権限: Mail.Send |
| Power Automate | HTTP Trigger | Webhook URL (Key Vault) |
