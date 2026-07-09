# 携帯電話事業：半導体供給不足インパクト判断スキル

## 概要

世界的な半導体供給不足が携帯電話事業に与えるインパクトを定量的に判断するスキル。端末調達遅延の深刻度、在庫逼迫レベル、販売機会損失、および顧客離反リスクを総合的に評価し、推奨アクションを出力する。

## 入力パラメータ

### ニュース情報

| パラメータ | 型 | 説明 |
|---|---|---|
| `news_category` | string | ニュースカテゴリ（`supply_chain`） |
| `affected_components` | list[string] | 影響を受ける半導体コンポーネント（例: `SoC`, `memory`, `display_driver`） |
| `supply_shortage_severity` | enum | `critical` / `severe` / `moderate` / `minor` |
| `estimated_delay_weeks` | int | 推定調達遅延期間（週） |
| `affected_manufacturers` | list[string] | 影響を受ける端末メーカー（例: `Apple`, `Samsung`, `Sony`） |
| `geographic_scope` | enum | `global` / `regional_asia` / `regional_other` |

### 業務データ

| パラメータ | 型 | 説明 |
|---|---|---|
| `current_inventory_weeks` | float | 現在の端末在庫週数 |
| `monthly_sales_volume` | int | 月間端末販売台数 |
| `backorder_count` | int | 現在のバックオーダー件数 |
| `affected_model_ratio` | float | 影響モデルの売上構成比（0.0〜1.0） |
| `alternative_supplier_count` | int | 代替調達先の数 |
| `mnp_churn_rate_current` | float | 直近MNP転出率（月次） |
| `avg_device_margin_jpy` | int | 端末1台あたり平均粗利（円） |

## 判断ロジック

### Step 1: 供給不足の重大性判定

```
IF supply_shortage_severity == "critical" AND geographic_scope == "global" THEN severity = "critical"
ELSE IF supply_shortage_severity == "critical" OR (supply_shortage_severity == "severe" AND geographic_scope == "global") THEN severity = "high"
ELSE IF supply_shortage_severity == "severe" OR supply_shortage_severity == "moderate" THEN severity = "medium"
ELSE severity = "low"
```

### Step 2: 在庫枯渇リスクの算出

```
weekly_demand = monthly_sales_volume / 4
inventory_coverage_ratio = current_inventory_weeks / estimated_delay_weeks

IF inventory_coverage_ratio < 0.3 THEN stockout_risk = "critical"  -- 在庫が遅延期間の30%未満
ELSE IF inventory_coverage_ratio < 0.6 THEN stockout_risk = "high"
ELSE IF inventory_coverage_ratio < 1.0 THEN stockout_risk = "medium"
ELSE stockout_risk = "low"  -- 在庫で遅延期間をカバー可能
```

### Step 3: 販売機会損失額の推定

```
shortage_weeks = MAX(0, estimated_delay_weeks - current_inventory_weeks)
lost_sales_volume = weekly_demand × shortage_weeks × affected_model_ratio
lost_revenue_jpy = lost_sales_volume × avg_device_margin_jpy
```

### Step 4: 顧客離反リスクの評価

```
-- 在庫切れ時のMNP転出率上昇を推定（過去データに基づく係数）
churn_multiplier:
  stockout_risk "critical" = 3.0  -- 転出率3倍に上昇
  stockout_risk "high" = 2.0
  stockout_risk "medium" = 1.4
  stockout_risk "low" = 1.0

estimated_churn_rate = mnp_churn_rate_current × churn_multiplier
estimated_churn_volume = monthly_sales_volume × estimated_churn_rate × (estimated_delay_weeks / 4)
```

### Step 5: 代替調達の緩和効果

```
IF alternative_supplier_count >= 3 THEN mitigation_factor = 0.5
ELSE IF alternative_supplier_count >= 1 THEN mitigation_factor = 0.75
ELSE mitigation_factor = 1.0  -- 緩和なし
```

### Step 6: 総合インパクトスコア算出

```
severity_weight:
  critical = 1.0
  high = 0.75
  medium = 0.45
  low = 0.15

stockout_weight:
  critical = 1.0
  high = 0.7
  medium = 0.4
  low = 0.1

raw_score = (severity_weight × 0.4 + stockout_weight × 0.4 + affected_model_ratio × 0.2) × mitigation_factor × 100
impact_score = clamp(raw_score, 0, 100)
```

### Step 7: インパクトレベル判定

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
| `stockout_risk_level` | enum | 在庫枯渇リスクレベル |
| `shortage_weeks` | int | 在庫不足予測期間（週） |
| `lost_revenue_estimate_jpy` | int | 販売機会損失推定額（円） |
| `churn_risk_volume` | int | 顧客離反予測数 |
| `recommended_actions` | list[string] | 推奨アクションリスト |
| `risk_summary` | string | リスクサマリー文（自然言語） |

### 推奨アクション分岐

| レベル | 推奨アクション |
|---|---|
| critical | 緊急調達会議の招集、代替メーカー（OPPO/Xiaomi等）との緊急交渉開始、在庫配分アルゴリズムの即時切替（解約リスク顧客優先）、端末予約・優先配送プログラムの発動、経営層エスカレーション |
| high | 代替端末メーカーへの発注拡大、中古端末リファービッシュ事業の拡大、通信プラン単体契約への誘導施策強化、バックオーダー顧客へのリテンション施策実行 |
| medium | 調達先ポートフォリオの見直し着手、在庫安全水準の引き上げ検討、影響モデルの販促抑制によるバーンレート低減 |
| low | サプライチェーン動向のモニタリング強化、次四半期調達計画のレビュー、定期レポートへの記載 |

## 参照データソース（Fabric テーブル）

| テーブル名 | 説明 | 主要カラム |
|---|---|---|
| `fact_inventory_snapshot` | 端末在庫スナップショット | `snapshot_date`, `terminal_id`, `quantity`, `warehouse_id`, `weeks_of_supply` |
| `fact_procurement_orders` | 端末調達発注実績 | `order_date`, `supplier_id`, `terminal_id`, `quantity`, `estimated_delivery_date`, `status` |
| `fact_sales_daily` | 日次販売実績 | `sales_date`, `terminal_id`, `channel`, `quantity`, `revenue` |
| `fact_mnp_events` | MNP転出入イベント | `event_date`, `customer_id`, `direction`, `reason_code`, `device_model` |
| `dim_terminals` | 端末マスタ | `terminal_id`, `maker`, `model_name`, `chipset`, `release_date`, `cost_tier` |
| `dim_suppliers` | サプライヤーマスタ | `supplier_id`, `country`, `lead_time_weeks`, `component_type`, `tier` |
| `fact_backorders` | バックオーダー管理 | `order_id`, `customer_id`, `terminal_id`, `order_date`, `expected_fulfillment_date` |
| `agg_monthly_device_sales` | 月次端末販売集計 | `month`, `terminal_id`, `total_quantity`, `avg_margin`, `model_revenue_share` |
