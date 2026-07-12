# SI事業：為替急変インパクト判断スキル

## 概要

為替レートの急変動（円安/円高）がSI事業に与えるインパクトを判断するスキル。オフショア開発費用、外貨建てクラウドインフラ契約、海外ベンダー製ライセンス費用の変動を定量評価し、プロジェクト収益・契約条件への影響度と推奨アクションを出力する。

## 入力パラメータ

### ニュース情報

| パラメータ | 型 | 説明 |
|---|---|---|
| `news_category` | string | ニュースカテゴリ（`economy`） |
| `currency_pair` | string | 通貨ペア（例: `USD/JPY`, `INR/JPY`） |
| `rate_change_percent` | float | 為替変動率（%） |
| `rate_direction` | enum | `yen_depreciation` / `yen_appreciation` |
| `time_horizon` | enum | `short_term` / `mid_term` / `long_term` |

### 業務データ

| パラメータ | 型 | 説明 |
|---|---|---|
| `offshore_cost_ratio` | float | オフショア開発費比率（0.0〜1.0） |
| `offshore_monthly_cost_jpy` | int | 月間オフショア開発費（円） |
| `cloud_foreign_cost_ratio` | float | 外貨建てクラウド費用比率（0.0〜1.0） |
| `monthly_cloud_cost_jpy` | int | 月間クラウドインフラ費用（円） |
| `license_foreign_cost_monthly` | int | 月間外貨建てライセンス費（円） |
| `active_projects_count` | int | 進行中プロジェクト数 |
| `foreign_currency_contracts` | int | 外貨建て契約数 |
| `hedge_coverage_ratio` | float | 為替ヘッジカバー率（0.0〜1.0） |
| `fixed_price_project_ratio` | float | 固定価格契約プロジェクト比率（0.0〜1.0） |
| `avg_project_margin` | float | 平均プロジェクト利益率（0.0〜1.0） |

## 判断ロジック

### Step 1: 為替変動の重大性判定

```
IF abs(rate_change_percent) >= 15.0 THEN severity = "critical"
ELSE IF abs(rate_change_percent) >= 8.0 THEN severity = "high"
ELSE IF abs(rate_change_percent) >= 3.0 THEN severity = "medium"
ELSE severity = "low"
```

### Step 2: オフショア開発費への影響算出

```
unhedged_exposure = offshore_cost_ratio × (1 - hedge_coverage_ratio)
offshore_cost_increase = offshore_monthly_cost_jpy × unhedged_exposure × (rate_change_percent / 100)
annual_offshore_impact = offshore_cost_increase × 12
```

### Step 3: クラウド・ライセンス費への影響算出

```
cloud_cost_increase = monthly_cloud_cost_jpy × cloud_foreign_cost_ratio × (rate_change_percent / 100)
license_cost_increase = license_foreign_cost_monthly × (rate_change_percent / 100)
annual_infra_impact = (cloud_cost_increase + license_cost_increase) × 12
```

### Step 4: プロジェクト収益性への影響

```
// 固定価格契約はコスト増を価格転嫁できないため影響大
total_annual_cost_increase = annual_offshore_impact + annual_infra_impact
margin_erosion = total_annual_cost_increase × fixed_price_project_ratio

// マージンが為替影響で消失するプロジェクト数の推定
IF avg_project_margin > 0 THEN
    margin_at_risk_ratio = (rate_change_percent / 100 × unhedged_exposure) / avg_project_margin
    projects_at_risk = active_projects_count × fixed_price_project_ratio × min(margin_at_risk_ratio, 1.0)
ELSE
    projects_at_risk = active_projects_count × fixed_price_project_ratio
```

### Step 5: 総合インパクトスコア算出

```
severity_weight:
  critical = 1.0, high = 0.7, medium = 0.4, low = 0.1

contract_risk_factor = fixed_price_project_ratio × (1 - hedge_coverage_ratio)

raw_score = (severity_weight × 0.3 + contract_risk_factor × 0.35 + (offshore_cost_ratio × unhedged_exposure) × 0.35) × 100

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
| `annual_offshore_impact_jpy` | int | 年間オフショア費影響額（円） |
| `annual_infra_impact_jpy` | int | 年間インフラ費影響額（円） |
| `total_annual_cost_increase_jpy` | int | 年間総コスト増加額（円） |
| `projects_at_risk` | int | 収益悪化リスクプロジェクト数 |
| `margin_erosion_jpy` | int | マージン侵食額（円） |
| `recommended_actions` | list[string] | 推奨アクションリスト |
| `risk_summary` | string | リスクサマリー文（自然言語） |

### 推奨アクション分岐

| レベル | 推奨アクション |
|---|---|
| critical | 外貨建て契約の即時再交渉（円建て切替・為替条項追加）、赤字リスクプロジェクトの緊急収支レビュー、為替ヘッジの追加手配、オフショア拠点の通貨分散検討、経営層へのエスカレーション |
| high | 新規契約への為替変動条項の標準化、オフショア比率の段階的見直し、クラウドベンダーとのリザーブドインスタンス契約拡大（コスト固定化） |
| medium | 外貨建てコストのモニタリング頻度引上げ、次四半期見積りへの為替バッファ組込み、ニアショア代替先の調査開始 |
| low | 定期レポートへの記載、プロジェクト管理部門との情報共有 |

## 参照データソース（Fabric テーブル）

| テーブル名 | 説明 | 主要カラム |
|---|---|---|
| `fact_project_costs` | プロジェクト費用実績 | `project_id`, `month`, `cost_category`, `currency`, `amount_local`, `amount_jpy` |
| `fact_offshore_contracts` | オフショア開発契約 | `contract_id`, `vendor_id`, `currency`, `monthly_amount`, `start_date`, `end_date` |
| `fact_cloud_billing` | クラウド利用料 | `month`, `provider`, `service`, `currency`, `amount_local`, `amount_jpy` |
| `fact_license_costs` | ライセンス費用 | `license_id`, `vendor`, `currency`, `annual_fee`, `renewal_date` |
| `fact_exchange_rates` | 為替レート履歴 | `date`, `currency_pair`, `rate`, `change_percent` |
| `dim_projects` | プロジェクトマスタ | `project_id`, `name`, `contract_type`, `margin_target`, `offshore_flag`, `status` |
| `fact_hedge_contracts` | 為替ヘッジ契約 | `contract_id`, `currency_pair`, `coverage_amount`, `expiry_date` |
