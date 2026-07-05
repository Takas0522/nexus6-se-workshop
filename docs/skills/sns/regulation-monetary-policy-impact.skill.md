# SNS事業：金融政策転換インパクト判断スキル

## 概要

中央銀行の金融政策転換（利上げ/利下げ）がSNS事業に与えるインパクトを判断するスキル。広告主企業の予算縮小による広告収入減少、消費者の可処分所得変動によるアプリ内課金への影響、自社の開発投資コスト変動を定量評価し、収益計画の見直し要否と推奨アクションを出力する。

## 入力パラメータ

### ニュース情報

| パラメータ | 型 | 説明 |
|---|---|---|
| `news_category` | string | ニュースカテゴリ（`regulation`） |
| `policy_direction` | enum | `rate_hike` / `rate_cut` |
| `rate_change_bps` | int | 政策金利変動幅（ベーシスポイント） |
| `new_policy_rate` | float | 変更後の政策金利（%） |
| `guidance_outlook` | enum | `further_tightening` / `neutral` / `easing` |
| `effective_date` | date | 政策適用日 |

### 業務データ

| パラメータ | 型 | 説明 |
|---|---|---|
| `ad_revenue_monthly` | int | 月間広告収入（円） |
| `ad_revenue_ratio` | float | 総売上に占める広告収入比率（0.0〜1.0） |
| `top10_advertisers_concentration` | float | 上位10社広告主の売上集中度（0.0〜1.0） |
| `iap_revenue_monthly` | int | 月間アプリ内課金収入（円） |
| `subscription_revenue_monthly` | int | 月間サブスクリプション収入（円） |
| `monthly_development_cost` | int | 月間開発費（円） |
| `debt_ratio` | float | 有利子負債比率（0.0〜1.0） |
| `advertiser_industry_mix` | map[string, float] | 広告主業種別構成比 |

## 判断ロジック

### Step 1: 金利変動の重大性判定

```
IF rate_change_bps >= 75 THEN severity = "critical"
ELSE IF rate_change_bps >= 50 THEN severity = "high"
ELSE IF rate_change_bps >= 25 THEN severity = "medium"
ELSE severity = "low"
```

### Step 2: 広告収入への影響算出

```
// 利上げ時: 広告主の予算縮小 → 広告収入減少
// 利下げ時: 広告主の予算拡大 → 広告収入増加
// 景気感応度の高い業種ほど影響大
sensitive_industries = ["retail", "travel", "real_estate", "automotive"]
sensitive_ratio = SUM(advertiser_industry_mix[i] for i in sensitive_industries)

ad_decline_rate =
    IF severity == "critical" THEN 0.15 × sensitive_ratio + 0.05
    ELSE IF severity == "high" THEN 0.10 × sensitive_ratio + 0.03
    ELSE IF severity == "medium" THEN 0.05 × sensitive_ratio + 0.01
    ELSE 0.02 × sensitive_ratio

// 広告主集中度が高いほどリスク増
concentration_amplifier = 1.0 + (top10_advertisers_concentration - 0.5) × 0.5
ad_decline_rate = ad_decline_rate × concentration_amplifier

projected_ad_revenue_loss = ad_revenue_monthly × ad_decline_rate × 12
```

### Step 3: 課金・サブスクリプション収入への影響

```
// 利上げ → 可処分所得減少 → 課金控え
// 利下げ → 可処分所得増加 → 課金増加
IF policy_direction == "rate_hike" THEN
    iap_decline_rate =
        IF rate_change_bps >= 50 THEN 0.08
        ELSE IF rate_change_bps >= 25 THEN 0.04
        ELSE 0.02
    subscription_churn_increase = iap_decline_rate × 0.5  // サブスクは粘着性高い
ELSE
    iap_decline_rate = -0.03  // 利下げでは微増
    subscription_churn_increase = -0.01

projected_iap_impact = iap_revenue_monthly × iap_decline_rate × 12
projected_sub_impact = subscription_revenue_monthly × subscription_churn_increase × 12
```

### Step 4: 自社コストへの影響

```
// 有利子負債の金利負担変動
interest_cost_change = monthly_development_cost × debt_ratio × (rate_change_bps / 10000) × 12
```

### Step 5: 総合インパクトスコア算出

```
total_annual_impact = projected_ad_revenue_loss + projected_iap_impact + projected_sub_impact + interest_cost_change

// 広告依存度が高いほどスコア上昇
ad_dependency_factor = ad_revenue_ratio × 1.5

severity_weight:
  critical = 1.0, high = 0.7, medium = 0.4, low = 0.1

outlook_multiplier:
  further_tightening = 1.4
  neutral = 1.0
  easing = 0.6

raw_score = (severity_weight × 0.3 + ad_decline_rate × 3.0 × 0.4 + ad_dependency_factor × 0.3) × outlook_multiplier × 100

impact_score = clamp(raw_score, 0, 100)

IF policy_direction == "rate_cut" THEN
    impact_direction = "positive"
ELSE
    impact_direction = "negative"
```

### Step 6: インパクトレベル判定

```
IF impact_score >= 80 THEN level = "critical"
ELSE IF impact_score >= 55 THEN level = "high"
ELSE IF impact_score >= 30 THEN level = "medium"
ELSE level = "low"
```

## 出力

| フィールド | 型 | 説明 |
|---|---|---|
| `impact_score` | int (0-100) | 総合インパクトスコア |
| `impact_level` | enum | `critical` / `high` / `medium` / `low` |
| `impact_direction` | enum | `negative` / `positive` |
| `projected_ad_revenue_loss_jpy` | int | 年間広告収入減少額（円） |
| `projected_iap_impact_jpy` | int | 年間課金収入影響額（円） |
| `projected_sub_impact_jpy` | int | 年間サブスク収入影響額（円） |
| `total_annual_impact_jpy` | int | 年間総影響額（円） |
| `recommended_actions` | list[string] | 推奨アクションリスト |
| `risk_summary` | string | リスクサマリー文（自然言語） |

### 推奨アクション分岐

| レベル | 推奨アクション（利上げ時） |
|---|---|
| critical | 広告単価の緊急見直し（ボリュームディスカウント導入）、中小広告主の新規獲得キャンペーン、サブスクリプション収益の強化施策発動、開発投資の優先順位再定義、経営層へのエスカレーション |
| high | 広告主ポートフォリオの分散化推進、成果報酬型広告メニューの拡充、課金コンテンツの付加価値強化、コスト構造の見直し着手 |
| medium | 広告主の予算動向ヒアリング強化、課金ユーザーのリテンション施策準備、次四半期収益シナリオの複数策定 |
| low | 定期レポートへの記載、マーケティング部門との情報共有 |

| レベル | 推奨アクション（利下げ時） |
|---|---|
| high (positive) | 広告単価の引上げ検討、新規広告プロダクトの開発加速、成長投資の前倒し実行 |
| medium (positive) | 広告主への積極的な予算増額提案、プレミアム課金コンテンツの拡充 |

## 参照データソース（Fabric テーブル）

| テーブル名 | 説明 | 主要カラム |
|---|---|---|
| `fact_ad_revenue` | 広告収入実績 | `date`, `advertiser_id`, `campaign_id`, `impressions`, `revenue_jpy`, `unit_price` |
| `fact_iap_transactions` | アプリ内課金実績 | `date`, `user_id`, `item_id`, `amount_jpy`, `payment_method` |
| `fact_subscription_events` | サブスク契約イベント | `date`, `user_id`, `plan_id`, `event_type`, `amount_jpy` |
| `dim_advertisers` | 広告主マスタ | `advertiser_id`, `name`, `industry`, `annual_budget_tier`, `contract_type` |
| `fact_interest_rate_history` | 金利推移履歴 | `date`, `rate_type`, `rate_value`, `change_bps` |
| `agg_advertiser_spend_monthly` | 広告主別月次出稿集計 | `month`, `advertiser_id`, `industry`, `spend_jpy`, `mom_change_rate` |
| `agg_revenue_breakdown` | 収益構成集計 | `month`, `revenue_type`, `amount_jpy`, `ratio` |
