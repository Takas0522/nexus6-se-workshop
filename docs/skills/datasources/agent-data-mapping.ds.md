# Hosted Agent データマッピング定義

## エージェント構成概要

ニュース分析エージェントシステムは4つのエージェントが連携してニュースの収集→評価→レコメンド→通知を実行するパイプラインで構成される。

```
[Agent 1: Web情報収集]
        │ news_articles (WRITE)
        ▼
[Agent 2: ビジネスインパクト評価]
        │ impact_assessments (WRITE)
        ▼
[Agent 3: 事業部別レコメンド]
        │ impact_assessments (UPDATE)
        ▼
[Agent 4: 通知]
        │ notifications (WRITE)
        ▼
    [Teams / Email]
```

---

## Agent 1: Web情報収集エージェント

### 役割

Bing Grounding を使用してWebからニュース記事を収集し、カテゴリ分類・構造化を行い保存する。

### データソースマッピング

| テーブル | アクセス種別 | 用途 |
|---|---|---|
| `sqldb_common_01.news_articles` | **WRITE** | 収集した記事の格納 |

### 外部データソース

| ソース | 種別 | 用途 |
|---|---|---|
| Bing Grounding API | READ | ニュース記事の検索・取得 |

### 取得クエリパターン

```sql
-- 重複チェック（同一記事の再取込防止）
SELECT article_id FROM sqldb_common_01.news_articles
WHERE title = @title AND source = @source AND published_at = @published_at;

-- 記事の挿入
INSERT INTO sqldb_common_01.news_articles
    (article_id, title, body, source, published_at, category, tags, ingested_at)
VALUES
    (@article_id, @title, @body, @source, @published_at, @category, @tags, GETUTCDATE());

-- 直近の取込状況確認（取込間隔制御用）
SELECT MAX(ingested_at) AS last_ingested
FROM sqldb_common_01.news_articles
WHERE category = @category;
```

### 出力スキーマ

| フィールド | 型 | Agent 2 への引渡し |
|---|---|---|
| `article_id` | UNIQUEIDENTIFIER | ✓ |
| `category` | NVARCHAR(50) | ✓（スキル選択に使用） |
| `title` | NVARCHAR(500) | ✓ |
| `body` | NVARCHAR(MAX) | ✓ |
| `published_at` | DATETIME2 | ✓ |

---

## Agent 2: ビジネスインパクト評価エージェント

### 役割

収集されたニュース記事に対し、各事業領域のスキルを適用してインパクトスコアを算出する。Fabric上のKPIデータを参照して定量評価を行う。

### データソースマッピング

| テーブル | アクセス種別 | 用途 |
|---|---|---|
| `sqldb_common_01.news_articles` | READ | 評価対象記事の取得 |
| `sqldb_common_01.impact_assessments` | **WRITE** | 診断結果の格納 |
| `sqldb_common_01.unified_customers` | READ | 影響顧客数の概算 |
| `sqldb_common_01.domain_id_mappings` | READ | 事業横断影響の判定 |
| `sqldb_mobile_01.contracts` | READ | 携帯契約KPI参照 |
| `sqldb_mobile_01.usage_billing` | READ | ARPU算出用 |
| `sqldb_mobile_01.mnp_history` | READ | 解約トレンド参照 |
| `sqldb_ecommerce_01.sns_users` | READ | MAU/DAU算出用 |
| `sqldb_ecommerce_01.ad_revenues` | READ | 広告収益トレンド参照 |
| `sqldb_fintech_01.si_projects` | READ | パイプライン・プロジェクト状況参照 |
| `sqldb_fintech_01.si_transactions` | READ | 取引実績参照 |

### 取得クエリパターン

```sql
-- 未評価記事の取得
SELECT a.article_id, a.title, a.body, a.category, a.published_at
FROM sqldb_common_01.news_articles a
WHERE NOT EXISTS (
    SELECT 1 FROM sqldb_common_01.impact_assessments ia
    WHERE ia.article_id = a.article_id
)
ORDER BY a.published_at DESC;

-- 携帯事業KPI取得（スキル入力用）
SELECT
    COUNT(*) AS subscriber_count,
    AVG(c.monthly_fee) AS avg_monthly_fee,
    SUM(CASE WHEN c.installment_flag = 1 THEN 1 ELSE 0 END) * 1.0 / COUNT(*) AS installment_ratio
FROM sqldb_mobile_01.contracts c
WHERE c.status = 'active';

-- 携帯MNP転出率（直近3ヶ月）
SELECT
    COUNT(*) * 1.0 / (SELECT COUNT(*) FROM sqldb_mobile_01.contracts WHERE status = 'active') AS mnp_out_rate
FROM sqldb_mobile_01.mnp_history
WHERE direction = 'out' AND processed_at >= DATEADD(MONTH, -3, GETUTCDATE());

-- SNS事業KPI取得
SELECT
    COUNT(DISTINCT CASE WHEN last_active_at >= DATEADD(DAY, -1, GETUTCDATE()) THEN user_id END) AS dau,
    COUNT(DISTINCT CASE WHEN last_active_at >= DATEADD(DAY, -30, GETUTCDATE()) THEN user_id END) AS mau
FROM sqldb_ecommerce_01.sns_users
WHERE status = 'active';

-- SNS広告収益トレンド（直近3ヶ月）
SELECT
    FORMAT(revenue_date, 'yyyy-MM') AS month,
    SUM(revenue_jpy) AS total_revenue,
    AVG(revenue_jpy / NULLIF(impressions, 0)) AS avg_unit_price
FROM sqldb_ecommerce_01.ad_revenues
WHERE revenue_date >= DATEADD(MONTH, -3, GETUTCDATE())
GROUP BY FORMAT(revenue_date, 'yyyy-MM');

-- SI事業パイプライン状況
SELECT
    status,
    COUNT(*) AS project_count,
    SUM(amount_oku) AS total_amount_oku,
    AVG(offshore_ratio) AS avg_offshore_ratio
FROM sqldb_fintech_01.si_projects
WHERE status IN ('pipeline', 'active', 'on_hold')
GROUP BY status;

-- SI事業 固定価格契約比率
SELECT
    SUM(CASE WHEN contract_type = 'fixed_price' THEN 1 ELSE 0 END) * 1.0 / COUNT(*) AS fixed_price_ratio
FROM sqldb_fintech_01.si_projects
WHERE status = 'active';

-- 診断結果の挿入
INSERT INTO sqldb_common_01.impact_assessments
    (assessment_id, article_id, domain, skill_id, impact_score, impact_level,
     impact_direction, risk_summary, recommended_actions, assessed_at)
VALUES
    (@assessment_id, @article_id, @domain, @skill_id, @impact_score, @impact_level,
     @impact_direction, @risk_summary, @recommended_actions, GETUTCDATE());
```

### スキルとデータの対応関係

| スキルID | 事業領域 | シナリオ | 参照テーブル |
|---|---|---|---|
| `mobile/economy-fx-impact` | 携帯電話 | 為替急変 | `contracts`, `usage_billing` |
| `mobile/competition-merger-impact` | 携帯電話 | 競合統合 | `contracts`, `mnp_history` |
| `mobile/regulation-monetary-policy-impact` | 携帯電話 | 金融政策転換 | `contracts`, `usage_billing` |
| `sns/economy-fx-impact` | SNS | 為替急変 | `ad_revenues`, `sns_users` |
| `sns/competition-merger-impact` | SNS | 競合統合 | `sns_users`, `sns_activities`, `ad_revenues` |
| `sns/regulation-monetary-policy-impact` | SNS | 金融政策転換 | `ad_revenues`, `sns_users` |
| `si/economy-fx-impact` | SI | 為替急変 | `si_projects`, `si_transactions` |
| `si/competition-merger-impact` | SI | 競合統合 | `si_projects`, `si_clients` |
| `si/regulation-monetary-policy-impact` | SI | 金融政策転換 | `si_projects`, `si_clients`, `si_transactions` |

### スキル選択ロジック

```
INPUT: article.category
OUTPUT: list[skill_id]

IF category == "economy" THEN
    skills = [
        "mobile/economy-fx-impact",
        "sns/economy-fx-impact",
        "si/economy-fx-impact"
    ]
ELSE IF category == "competition" THEN
    skills = [
        "mobile/competition-merger-impact",
        "sns/competition-merger-impact",
        "si/competition-merger-impact"
    ]
ELSE IF category == "regulation" THEN
    skills = [
        "mobile/regulation-monetary-policy-impact",
        "sns/regulation-monetary-policy-impact",
        "si/regulation-monetary-policy-impact"
    ]
```

---

## Agent 3: 事業部別レコメンドエージェント

### 役割

インパクト評価結果に基づき、各事業部門の業務コンテキストを加味した具体的アクションレコメンドを生成する。

### データソースマッピング

| テーブル | アクセス種別 | 用途 |
|---|---|---|
| `sqldb_common_01.impact_assessments` | READ / **UPDATE** | 診断結果の参照・推奨アクション追記 |
| `sqldb_common_01.unified_customers` | READ | 影響顧客の特定 |
| `sqldb_common_01.customer_segments` | READ | セグメント別対応策の策定 |
| `sqldb_common_01.domain_id_mappings` | READ | 事業横断顧客の特定 |
| `sqldb_mobile_01.customers` | READ | 携帯顧客属性参照 |
| `sqldb_mobile_01.contracts` | READ | 契約内容による影響範囲特定 |
| `sqldb_mobile_01.mnp_history` | READ | 過去の転出パターン分析 |
| `sqldb_ecommerce_01.sns_users` | READ | ユーザーセグメント参照 |
| `sqldb_ecommerce_01.sns_activities` | READ | エンゲージメント状況参照 |
| `sqldb_ecommerce_01.ad_revenues` | READ | 広告主別影響分析 |
| `sqldb_fintech_01.si_clients` | READ | クライアント業種・規模参照 |
| `sqldb_fintech_01.si_projects` | READ | 案件別リスク詳細分析 |
| `sqldb_fintech_01.si_transactions` | READ | 取引履歴による優先度判定 |

### 取得クエリパターン

```sql
-- high以上の診断結果取得（レコメンド対象）
SELECT ia.*, na.title, na.category
FROM sqldb_common_01.impact_assessments ia
JOIN sqldb_common_01.news_articles na ON ia.article_id = na.article_id
WHERE ia.impact_level IN ('critical', 'high')
  AND ia.recommended_actions IS NULL
ORDER BY ia.impact_score DESC;

-- 携帯事業：影響を受ける顧客セグメント特定
SELECT cs.segment_id, cs.segment_name, cs.customer_count
FROM sqldb_common_01.customer_segments cs
WHERE cs.domain IN ('mobile', 'all')
  AND JSON_VALUE(cs.criteria_json, '$.installment_flag') = 'true';

-- 携帯事業：割賦契約中で残月数が長い顧客数（金融政策転換時）
SELECT COUNT(*) AS affected_customers
FROM sqldb_mobile_01.contracts
WHERE installment_flag = 1 AND status = 'active'
  AND installment_months - DATEDIFF(MONTH, contract_start, GETUTCDATE()) > 12;

-- SNS事業：影響を受ける広告主の特定（業種別）
SELECT advertiser_industry, COUNT(DISTINCT advertiser_id) AS advertiser_count,
       SUM(revenue_jpy) AS total_revenue
FROM sqldb_ecommerce_01.ad_revenues
WHERE revenue_date >= DATEADD(MONTH, -1, GETUTCDATE())
GROUP BY advertiser_industry
ORDER BY total_revenue DESC;

-- SNS事業：解約リスクの高いプレミアムユーザー
SELECT COUNT(*) AS at_risk_premium_users
FROM sqldb_ecommerce_01.sns_users
WHERE subscription_plan = 'premium'
  AND last_active_at < DATEADD(DAY, -7, GETUTCDATE());

-- SI事業：凍結リスクのある案件一覧
SELECT p.project_id, p.project_name, p.amount_oku, p.contract_type,
       c.company_name, c.industry
FROM sqldb_fintech_01.si_projects p
JOIN sqldb_fintech_01.si_clients c ON p.client_id = c.client_id
WHERE p.status IN ('pipeline', 'active')
  AND p.project_type = 'new_development'
ORDER BY p.amount_oku DESC;

-- SI事業：事業横断で影響を受ける顧客の特定
SELECT uc.unified_customer_id, uc.customer_name,
       COUNT(DISTINCT dm.domain) AS affected_domains
FROM sqldb_common_01.unified_customers uc
JOIN sqldb_common_01.domain_id_mappings dm ON uc.unified_customer_id = dm.unified_customer_id
WHERE dm.domain IN ('mobile', 'sns', 'si')
GROUP BY uc.unified_customer_id, uc.customer_name
HAVING COUNT(DISTINCT dm.domain) >= 2;

-- レコメンド結果の更新
UPDATE sqldb_common_01.impact_assessments
SET recommended_actions = @recommended_actions
WHERE assessment_id = @assessment_id;
```

---

## Agent 4: 通知エージェント

### 役割

インパクト評価・レコメンド結果に基づき、適切な担当者・チームにTeams/Emailで通知を送信する。

### データソースマッピング

| テーブル | アクセス種別 | 用途 |
|---|---|---|
| `sqldb_common_01.impact_assessments` | READ | 通知内容の生成元 |
| `sqldb_common_01.notifications` | **WRITE** | 通知履歴の記録 |
| `sqldb_common_01.news_articles` | READ | 通知に含める記事情報 |

### 外部サービス

| サービス | 種別 | 用途 |
|---|---|---|
| Microsoft Teams (Webhook / Graph API) | WRITE | 通知メッセージ送信 |
| Microsoft 365 Email (Graph API) | WRITE | メール通知送信 |

### 取得クエリパターン

```sql
-- 未通知の診断結果取得
SELECT ia.assessment_id, ia.domain, ia.impact_score, ia.impact_level,
       ia.risk_summary, ia.recommended_actions,
       na.title AS article_title, na.published_at
FROM sqldb_common_01.impact_assessments ia
JOIN sqldb_common_01.news_articles na ON ia.article_id = na.article_id
WHERE NOT EXISTS (
    SELECT 1 FROM sqldb_common_01.notifications n
    WHERE n.assessment_id = ia.assessment_id
)
AND ia.impact_level IN ('critical', 'high')
ORDER BY ia.impact_score DESC;

-- 通知履歴の挿入
INSERT INTO sqldb_common_01.notifications
    (notification_id, assessment_id, recipient_domain, recipient_role,
     channel, priority, sent_at)
VALUES
    (@notification_id, @assessment_id, @recipient_domain, @recipient_role,
     @channel, @priority, GETUTCDATE());

-- 通知確認の更新
UPDATE sqldb_common_01.notifications
SET acknowledged_at = GETUTCDATE()
WHERE notification_id = @notification_id;

-- 通知抑制チェック（同一記事への重複通知防止）
SELECT COUNT(*) AS existing_notifications
FROM sqldb_common_01.notifications n
JOIN sqldb_common_01.impact_assessments ia ON n.assessment_id = ia.assessment_id
WHERE ia.article_id = @article_id AND n.recipient_domain = @domain
  AND n.sent_at >= DATEADD(HOUR, -1, GETUTCDATE());
```

### 通知ルーティング

| impact_level | domain | recipient_role | channel | priority |
|---|---|---|---|---|
| critical | mobile | 事業部長, 経営企画 | teams, email | critical |
| critical | sns | 事業部長, 経営企画 | teams, email | critical |
| critical | si | 事業部長, 経営企画 | teams, email | critical |
| high | mobile | 部門マネージャー | teams | high |
| high | sns | 部門マネージャー | teams | high |
| high | si | 部門マネージャー, PMO | teams | high |
| medium | mobile | 担当リーダー | teams | medium |
| medium | sns | 担当リーダー | teams | medium |
| medium | si | 担当リーダー | teams | medium |
| low | all | — | — (レポートのみ) | low |

---

## データフロー全体図

```
┌─────────────────────────────────────────────────────────────────┐
│                        外部データ                                 │
│  [Bing Grounding API] ─── ニュース記事 ───┐                     │
└───────────────────────────────────────────┼─────────────────────┘
                                            ▼
┌─────────────────────────────────────────────────────────────────┐
│  Agent 1: Web情報収集                                            │
│  WRITE → news_articles                                          │
└───────────────────────────────────────────┬─────────────────────┘
                                            ▼
┌─────────────────────────────────────────────────────────────────┐
│  Agent 2: ビジネスインパクト評価                                   │
│  READ  ← news_articles                                          │
│  READ  ← contracts, usage_billing, mnp_history    (携帯KPI)      │
│  READ  ← sns_users, sns_activities, ad_revenues  (SNS KPI)      │
│  READ  ← si_projects, si_clients, si_transactions (SI KPI)      │
│  READ  ← unified_customers, domain_id_mappings   (横断分析)      │
│  WRITE → impact_assessments                                      │
└───────────────────────────────────────────┬─────────────────────┘
                                            ▼
┌─────────────────────────────────────────────────────────────────┐
│  Agent 3: 事業部別レコメンド                                      │
│  READ  ← impact_assessments                                      │
│  READ  ← customer_segments, unified_customers     (顧客分析)     │
│  READ  ← 各事業領域テーブル（全テーブル）          (詳細分析)      │
│  UPDATE → impact_assessments (recommended_actions)               │
└───────────────────────────────────────────┬─────────────────────┘
                                            ▼
┌─────────────────────────────────────────────────────────────────┐
│  Agent 4: 通知                                                   │
│  READ  ← impact_assessments, news_articles                       │
│  WRITE → notifications                                           │
│  WRITE → [Microsoft Teams] / [Email]                             │
└─────────────────────────────────────────────────────────────────┘
```

### テーブル別アクセス権限サマリー

| テーブル | Agent 1 | Agent 2 | Agent 3 | Agent 4 |
|---|---|---|---|---|
| `news_articles` | **W** | R | — | R |
| `impact_assessments` | — | **W** | R/U | R |
| `notifications` | — | — | — | **W** |
| `unified_customers` | — | R | R | — |
| `domain_id_mappings` | — | R | R | — |
| `customer_segments` | — | — | R | — |
| `mobile_01.customers` | — | — | R | — |
| `mobile_01.contracts` | — | R | R | — |
| `mobile_01.usage_billing` | — | R | — | — |
| `mobile_01.mnp_history` | — | R | R | — |
| `ecommerce_01.sns_users` | — | R | R | — |
| `ecommerce_01.sns_activities` | — | — | R | — |
| `ecommerce_01.ad_revenues` | — | R | R | — |
| `fintech_01.si_clients` | — | — | R | — |
| `fintech_01.si_projects` | — | R | R | — |
| `fintech_01.si_transactions` | — | R | R | — |

> R = READ, W = WRITE, U = UPDATE, — = アクセスなし
