# SNS事業：為替急変インパクト判断スキル

## 概要

為替レートの急変動（円安/円高）がSNS事業に与えるインパクトを判断するスキル。海外プラットフォームへの広告出稿費用、外貨建てインフラコスト（クラウド・CDN）、海外売上の円換算変動を定量評価し、広告予算・収益計画への影響度と推奨アクションを出力する。

## 入力パラメータ

### ニュース情報

| パラメータ | 型 | 説明 |
|---|---|---|
| `news_category` | string | ニュースカテゴリ（`economy`） |
| `currency_pair` | string | 通貨ペア（例: `USD/JPY`） |
| `rate_change_percent` | float | 為替変動率（%） |
| `rate_direction` | enum | `yen_depreciation` / `yen_appreciation` |
| `time_horizon` | enum | `short_term` / `mid_term` / `long_term` |

### 業務データ

| パラメータ | 型 | 説明 |
|---|---|---|
| `foreign_ad_spend_ratio` | float | 海外プラットフォーム広告費比率（0.0〜1.0） |
| `monthly_ad_spend_jpy` | int | 月間広告出稿費（円） |
| `foreign_infra_cost_ratio` | float | 外貨建てインフラ費用比率（0.0〜1.0） |
| `monthly_infra_cost_jpy` | int | 月間インフラ費用（円） |
| `overseas_revenue_ratio` | float | 海外売上比率（0.0〜1.0） |
| `monthly_total_revenue_jpy` | int | 月間総売上（円） |
| `hedge_coverage_ratio` | float | 為替ヘッジカバー率（0.0〜1.0） |
| `ad_budget_flexibility` | enum | `fixed` / `adjustable` / `fully_flexible` |

## 判断ロジック

### Step 1: 為替変動の重大性判定

```
IF abs(rate_change_percent) >= 15.0 THEN severity = "critical"
ELSE IF abs(rate_change_percent) >= 8.0 THEN severity = "high"
ELSE IF abs(rate_change_percent) >= 3.0 THEN severity = "medium"
ELSE severity = "low"
```

### Step 2: 広告費影響の算出

```
unhedged_exposure = foreign_ad_spend_ratio × (1 - hedge_coverage_ratio)
ad_cost_increase = monthly_ad_spend_jpy × unhedged_exposure × (rate_change_percent / 100)
annual_ad_cost_impact = ad_cost_increase × 12

// 円安時: 海外広告費が増加（ネガティブ）
// 円高時: 海外広告費が減少（ポジティブ）
IF rate_direction == "yen_depreciation" THEN
    ad_impact_direction = "negative"
ELSE
    ad_impact_direction = "positive"
```

### Step 3: インフラコスト影響の算出

```
infra_cost_increase = monthly_infra_cost_jpy × foreign_infra_cost_ratio × (rate_change_percent / 100)
annual_infra_cost_impact = infra_cost_increase × 12
```

### Step 4: 海外売上への影響（円換算）

```
// 円安時: 海外売上の円換算額が増加（ポジティブ）
// 円高時: 海外売上の円換算額が減少（ネガティブ）
revenue_fx_effect = monthly_total_revenue_jpy × overseas_revenue_ratio × (rate_change_percent / 100)
annual_revenue_fx_effect = revenue_fx_effect × 12

IF rate_direction == "yen_depreciation" THEN
    revenue_impact_direction = "positive"
ELSE
    revenue_impact_direction = "negative"
```

### Step 5: 純影響額と総合スコア算出

```
// 円安時: コスト増 - 売上増
// 円高時: コスト減 - 売上減
net_annual_impact = (annual_ad_cost_impact + annual_infra_cost_impact) - annual_revenue_fx_effect

budget_flexibility_factor:
  fixed = 1.0
  adjustable = 0.7
  fully_flexible = 0.4

severity_weight:
  critical = 1.0, high = 0.7, medium = 0.4, low = 0.1

raw_score = severity_weight × unhedged_exposure × budget_flexibility_factor × 100
impact_score = clamp(raw_score, 0, 100)
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
| `net_annual_impact_jpy` | int | 年間純影響額（円、正=コスト増） |
| `annual_ad_cost_impact_jpy` | int | 年間広告費影響額（円） |
| `annual_infra_cost_impact_jpy` | int | 年間インフラ費影響額（円） |
| `annual_revenue_fx_effect_jpy` | int | 年間売上為替効果（円） |
| `recommended_actions` | list[string] | 推奨アクションリスト |
| `risk_summary` | string | リスクサマリー文（自然言語） |

### 推奨アクション分岐

| レベル | 推奨アクション（円安時） |
|---|---|
| critical | 海外広告出稿の即時縮小・国内プラットフォームへの振替、インフラ契約の円建て切替交渉、為替ヘッジの緊急追加、経営層へのエスカレーション |
| high | 広告予算の再配分計画策定、CDN/クラウドベンダーとの価格再交渉、海外売上拡大による自然ヘッジ強化 |
| medium | 広告ROIの通貨別モニタリング強化、次四半期予算のシナリオ別策定 |
| low | 定期レポートへの記載、財務部門との情報共有 |

| レベル | 推奨アクション（円高時） |
|---|---|
| high (positive) | 海外広告出稿の積極拡大、グローバル展開の加速検討 |
| medium (positive) | コスト低下分の再投資先検討、海外売上減への対策モニタリング |

## 参照データソース（Fabric テーブル）

| テーブル名 | 説明 | 主要カラム |
|---|---|---|
| `fact_ad_spend` | 広告出稿実績 | `date`, `platform`, `currency`, `amount_local`, `amount_jpy`, `campaign_id` |
| `fact_infra_costs` | インフラ費用実績 | `month`, `vendor`, `service_type`, `currency`, `amount_local`, `amount_jpy` |
| `fact_overseas_revenue` | 海外売上実績 | `month`, `region`, `currency`, `revenue_local`, `revenue_jpy` |
| `fact_exchange_rates` | 為替レート履歴 | `date`, `currency_pair`, `rate`, `change_percent` |
| `fact_hedge_contracts` | 為替ヘッジ契約 | `contract_id`, `currency_pair`, `coverage_amount`, `expiry_date` |
| `dim_ad_platforms` | 広告プラットフォームマスタ | `platform_id`, `name`, `billing_currency`, `category` |
| `agg_monthly_pnl` | 月次損益集計 | `month`, `revenue_domestic`, `revenue_overseas`, `cost_domestic`, `cost_overseas`, `operating_profit` |
