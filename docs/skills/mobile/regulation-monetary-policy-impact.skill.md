# 携帯電話事業：金融政策転換インパクト判断スキル

## 概要

中央銀行の金融政策転換（利上げ/利下げ）が携帯電話事業に与えるインパクトを判断するスキル。端末割賦販売への影響、設備投資コストの変動、消費者の端末購買行動の変化を定量評価し、事業計画の見直し要否と推奨アクションを出力する。

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
| `installment_sales_ratio` | float | 割賦販売比率（0.0〜1.0） |
| `avg_installment_months` | int | 平均分割回数（月） |
| `current_installment_rate` | float | 現行割賦金利（%） |
| `capex_planned_annual` | int | 年間設備投資計画額（億円） |
| `capex_debt_ratio` | float | 設備投資のうち借入依存比率（0.0〜1.0） |
| `subscriber_count` | int | 契約者数 |
| `installment_active_contracts` | int | 割賦販売中の契約数 |
| `avg_terminal_price` | int | 端末平均販売価格（円） |

## 判断ロジック

### Step 1: 金利変動の重大性判定

```
IF rate_change_bps >= 75 THEN severity = "critical"
ELSE IF rate_change_bps >= 50 THEN severity = "high"
ELSE IF rate_change_bps >= 25 THEN severity = "medium"
ELSE severity = "low"
```

### Step 2: 割賦販売への影響算出

```
new_installment_rate = current_installment_rate + (rate_change_bps / 100)
monthly_payment_increase = avg_terminal_price × (new_installment_rate - current_installment_rate) / 100 / avg_installment_months

// 消費者負担増による販売減少予測
IF monthly_payment_increase >= 500 THEN demand_decline_factor = 0.20
ELSE IF monthly_payment_increase >= 300 THEN demand_decline_factor = 0.12
ELSE IF monthly_payment_increase >= 100 THEN demand_decline_factor = 0.05
ELSE demand_decline_factor = 0.02

projected_sales_decline = installment_active_contracts × installment_sales_ratio × demand_decline_factor
```

### Step 3: 設備投資コスト影響算出

```
additional_interest_cost = capex_planned_annual × capex_debt_ratio × (rate_change_bps / 10000)
// 億円単位

IF additional_interest_cost >= 50 THEN capex_risk = "critical"
ELSE IF additional_interest_cost >= 20 THEN capex_risk = "high"
ELSE IF additional_interest_cost >= 5 THEN capex_risk = "medium"
ELSE capex_risk = "low"
```

### Step 4: 将来見通しによる増幅

```
outlook_multiplier:
  further_tightening = 1.5  // 追加利上げ示唆
  neutral = 1.0
  easing = 0.5             // 利下げ方向の場合は緩和

// 利下げシナリオでは影響がプラスに転じるケースあり
IF policy_direction == "rate_cut" THEN
    impact_direction = "positive"
    outlook_multiplier = inverse(outlook_multiplier)
ELSE
    impact_direction = "negative"
```

### Step 5: 総合インパクトスコア算出

```
severity_weight:
  critical = 1.0, high = 0.7, medium = 0.4, low = 0.1

capex_weight:
  critical = 1.0, high = 0.7, medium = 0.4, low = 0.1

raw_score = (severity_weight × 0.3 + capex_weight × 0.3 + demand_decline_factor × 2.0 × 0.4) × outlook_multiplier × 100

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
| `impact_direction` | enum | `negative` / `positive` |
| `monthly_payment_increase_jpy` | int | 月額支払増加額（円） |
| `projected_sales_decline_units` | int | 予測販売減少台数 |
| `additional_interest_cost_oku` | float | 追加金利コスト（億円） |
| `recommended_actions` | list[string] | 推奨アクションリスト |
| `risk_summary` | string | リスクサマリー文（自然言語） |

### 推奨アクション分岐

| レベル | 推奨アクション（利上げ時） |
|---|---|
| critical | 割賦金利の自社吸収方針決定、端末レンタルプランへの緊急移行促進、設備投資計画の即時見直し（優先順位再定義）、経営層へのエスカレーション |
| high | 割賦条件の改定シミュレーション実施、低価格端末ラインナップの拡充、5G投資の段階的実行への切替検討 |
| medium | 消費者購買動向のモニタリング強化、次四半期の端末販売計画レビュー、金利動向に応じた複数シナリオ策定 |
| low | 定期レポートへの記載、財務部門との情報共有 |

| レベル | 推奨アクション（利下げ時） |
|---|---|
| high (positive) | 割賦販売の積極プロモーション展開、設備投資の前倒し実行検討、高価格帯端末の販促強化 |
| medium (positive) | 割賦条件の改善による競争力訴求、投資計画の上方修正検討 |

## 参照データソース（Fabric テーブル）

| テーブル名 | 説明 | 主要カラム |
|---|---|---|
| `fact_installment_contracts` | 割賦販売契約 | `contract_id`, `subscriber_id`, `terminal_id`, `monthly_payment`, `remaining_months`, `interest_rate` |
| `fact_terminal_sales` | 端末販売実績 | `sale_date`, `terminal_id`, `sale_type`, `price`, `payment_method` |
| `fact_capex_budget` | 設備投資予算・実績 | `fiscal_year`, `quarter`, `category`, `planned_amount`, `actual_amount`, `funding_source` |
| `fact_interest_rate_history` | 金利推移履歴 | `date`, `rate_type`, `rate_value`, `change_bps` |
| `dim_terminals` | 端末マスタ | `terminal_id`, `maker`, `price_tier`, `recommended_retail_price` |
| `dim_capex_categories` | 設備投資カテゴリ | `category_id`, `name`, `priority`, `deferrable_flag` |
| `agg_consumer_demand_index` | 消費者需要指数 | `month`, `segment`, `demand_index`, `price_sensitivity` |
