# 携帯電話事業：為替急変インパクト判断スキル

## 概要

為替レートの急変動（円安/円高）が携帯電話事業に与えるインパクトを定量的に判断するスキル。海外メーカーからの端末仕入れコスト変動を中心に、調達計画・販売価格・収益への影響度を評価し、推奨アクションを出力する。

## 入力パラメータ

### ニュース情報

| パラメータ | 型 | 説明 |
|---|---|---|
| `news_category` | string | ニュースカテゴリ（`economy`） |
| `currency_pair` | string | 通貨ペア（例: `USD/JPY`, `EUR/JPY`） |
| `rate_change_percent` | float | 為替変動率（%） |
| `rate_direction` | enum | `yen_depreciation` / `yen_appreciation` |
| `time_horizon` | enum | `short_term` / `mid_term` / `long_term` |

### 業務データ

| パラメータ | 型 | 説明 |
|---|---|---|
| `foreign_procurement_ratio` | float | 海外調達比率（0.0〜1.0） |
| `current_inventory_months` | float | 現在の端末在庫月数 |
| `hedge_coverage_ratio` | float | 為替ヘッジカバー率（0.0〜1.0） |
| `contract_currency` | string | 主要契約通貨 |
| `quarterly_procurement_volume` | int | 四半期端末調達台数 |
| `avg_unit_cost_jpy` | int | 端末平均仕入単価（円） |

## 判断ロジック

### Step 1: 為替変動の重大性判定

```
IF abs(rate_change_percent) >= 15.0 THEN severity = "critical"
ELSE IF abs(rate_change_percent) >= 8.0 THEN severity = "high"
ELSE IF abs(rate_change_percent) >= 3.0 THEN severity = "medium"
ELSE severity = "low"
```

### Step 2: 事業影響度の算出

```
exposure = foreign_procurement_ratio × (1 - hedge_coverage_ratio)
cost_impact_per_unit = avg_unit_cost_jpy × (rate_change_percent / 100) × exposure
quarterly_cost_impact = cost_impact_per_unit × quarterly_procurement_volume
```

### Step 3: 在庫バッファ評価

```
IF current_inventory_months >= 3.0 THEN buffer_factor = 0.3
ELSE IF current_inventory_months >= 1.5 THEN buffer_factor = 0.6
ELSE buffer_factor = 1.0
```

### Step 4: 総合インパクトスコア算出

```
raw_score = severity_weight × exposure × buffer_factor × 100

severity_weight:
  critical = 1.0
  high = 0.7
  medium = 0.4
  low = 0.1

impact_score = clamp(raw_score, 0, 100)
```

### Step 5: インパクトレベル判定

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
| `quarterly_cost_impact_jpy` | int | 四半期コスト影響額（円） |
| `cost_impact_per_unit_jpy` | int | 1台あたりコスト増減額（円） |
| `recommended_actions` | list[string] | 推奨アクションリスト |
| `risk_summary` | string | リスクサマリー文（自然言語） |

### 推奨アクション分岐

| レベル | 推奨アクション |
|---|---|
| critical | 緊急調達会議の招集、販売価格の即時改定検討、国内代替サプライヤーへの切替交渉開始、経営層へのエスカレーション |
| high | 調達先の多様化検討、為替ヘッジ比率の引上げ、端末ラインナップの見直し着手 |
| medium | 為替動向のモニタリング強化、次四半期調達計画の前倒しレビュー |
| low | 定期レポートへの記載、次回定例会議でのアジェンダ追加 |

## 参照データソース（Fabric テーブル）

| テーブル名 | 説明 | 主要カラム |
|---|---|---|
| `fact_procurement_orders` | 端末調達発注実績 | `order_date`, `supplier_id`, `currency`, `unit_cost`, `quantity` |
| `fact_exchange_rates` | 為替レート履歴 | `date`, `currency_pair`, `rate`, `change_percent` |
| `dim_suppliers` | サプライヤーマスタ | `supplier_id`, `country`, `contract_currency`, `lead_time_days` |
| `dim_terminals` | 端末マスタ | `terminal_id`, `maker`, `origin_country`, `cost_tier` |
| `fact_inventory_snapshot` | 端末在庫スナップショット | `snapshot_date`, `terminal_id`, `quantity`, `warehouse_id` |
| `fact_hedge_contracts` | 為替ヘッジ契約 | `contract_id`, `currency_pair`, `coverage_amount`, `expiry_date` |
| `agg_quarterly_procurement` | 四半期調達集計 | `quarter`, `total_volume`, `avg_unit_cost`, `foreign_ratio` |
