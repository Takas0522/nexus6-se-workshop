# SI事業：半導体供給不足インパクト判断スキル

## 概要

世界的な半導体供給不足がSI事業に与えるインパクトを判断するスキル。顧客向けインフラ構築案件におけるサーバー・ネットワーク機器・GPU等のハードウェア調達遅延を中心に、プロジェクト納期リスク、追加コスト発生、案件パイプラインへの影響を定量的に評価し、推奨アクションを出力する。

## 入力パラメータ

### ニュース情報

| パラメータ | 型 | 説明 |
|---|---|---|
| `news_category` | string | ニュースカテゴリ（`supply_chain`） |
| `affected_components` | list[string] | 影響を受けるコンポーネント（例: `server_cpu`, `gpu`, `network_asic`, `storage_controller`, `memory`） |
| `supply_shortage_severity` | enum | `critical` / `severe` / `moderate` / `minor` |
| `estimated_delay_weeks` | int | 推定調達遅延期間（週） |
| `affected_vendors` | list[string] | 影響を受けるベンダー（例: `Intel`, `AMD`, `NVIDIA`, `Cisco`, `HPE`） |
| `geographic_scope` | enum | `global` / `regional_asia` / `regional_other` |

### 業務データ

| パラメータ | 型 | 説明 |
|---|---|---|
| `active_projects_count` | int | 進行中プロジェクト数 |
| `hw_dependent_project_ratio` | float | ハードウェア調達を含むプロジェクト比率（0.0〜1.0） |
| `projects_in_procurement_phase` | int | 調達フェーズにあるプロジェクト数 |
| `avg_project_value_jpy` | int | プロジェクト平均契約額（円） |
| `avg_hw_cost_ratio` | float | プロジェクトにおけるHWコスト比率（0.0〜1.0） |
| `pipeline_value_jpy` | int | 案件パイプライン総額（円） |
| `buffer_stock_weeks` | float | 戦略的バッファ在庫（週） |
| `cloud_migration_proposal_ratio` | float | クラウド移行提案の比率（0.0〜1.0） |
| `penalty_clause_project_count` | int | 遅延ペナルティ条項付きプロジェクト数 |
| `avg_penalty_per_week_jpy` | int | 1週間遅延あたりの平均ペナルティ額（円） |

## 判断ロジック

### Step 1: 影響範囲の特定

```
-- 影響を受けるプロジェクト数の推定
affected_project_count = active_projects_count × hw_dependent_project_ratio

-- コンポーネント種別による影響の重み付け
component_criticality:
  gpu = 1.0            -- AI/ML案件で代替困難
  server_cpu = 0.8     -- 汎用だが大量調達で影響大
  network_asic = 0.7   -- ネットワーク更改案件に直撃
  storage_controller = 0.5
  memory = 0.4         -- 比較的代替が容易

max_component_criticality = MAX(component_criticality[c] FOR c IN affected_components)
```

### Step 2: プロジェクト納期リスクの算出

```
-- バッファ在庫による緩和
effective_delay_weeks = MAX(0, estimated_delay_weeks - buffer_stock_weeks)

IF effective_delay_weeks >= 12 THEN schedule_risk = "critical"   -- 3ヶ月超の遅延
ELSE IF effective_delay_weeks >= 8 THEN schedule_risk = "high"
ELSE IF effective_delay_weeks >= 4 THEN schedule_risk = "medium"
ELSE schedule_risk = "low"

-- ペナルティリスク額
penalty_exposure_jpy = penalty_clause_project_count × avg_penalty_per_week_jpy × effective_delay_weeks
```

### Step 3: 財務影響の算出

```
-- 調達コスト増大（供給不足時のプレミアム）
cost_premium_ratio:
  critical = 0.30    -- 30%のプレミアム
  severe = 0.20
  moderate = 0.10
  minor = 0.05

hw_cost_increase_per_project = avg_project_value_jpy × avg_hw_cost_ratio × cost_premium_ratio
total_cost_increase_jpy = hw_cost_increase_per_project × projects_in_procurement_phase

-- プロジェクト利益率への影響
margin_erosion_jpy = total_cost_increase_jpy + penalty_exposure_jpy

-- パイプラインリスク（HW調達不安による受注辞退/延期）
IF schedule_risk IN ("critical", "high") THEN
    pipeline_risk_ratio = 0.15  -- パイプラインの15%が延期/辞退リスク
ELSE IF schedule_risk == "medium" THEN
    pipeline_risk_ratio = 0.08
ELSE
    pipeline_risk_ratio = 0.02

pipeline_at_risk_jpy = pipeline_value_jpy × pipeline_risk_ratio × hw_dependent_project_ratio
```

### Step 4: クラウド代替の緩和効果

```
-- クラウド移行提案によるリスク軽減
cloud_mitigation_factor = cloud_migration_proposal_ratio × 0.6
-- クラウド提案比率が高いほど、HW調達依存度が低い

effective_vulnerability = (1.0 - cloud_mitigation_factor) × hw_dependent_project_ratio
```

### Step 5: 顧客関係リスクの評価

```
-- 納期遅延による顧客信頼低下
IF effective_delay_weeks >= 8 AND penalty_clause_project_count >= 3 THEN
    customer_relationship_risk = "high"
ELSE IF effective_delay_weeks >= 4 OR penalty_clause_project_count >= 1 THEN
    customer_relationship_risk = "medium"
ELSE
    customer_relationship_risk = "low"

relationship_weight:
  high = 0.8
  medium = 0.4
  low = 0.1
```

### Step 6: 総合インパクトスコア算出

```
severity_weight:
  critical = 1.0
  high = 0.75
  medium = 0.45
  low = 0.15

schedule_weight:
  critical = 1.0
  high = 0.7
  medium = 0.4
  low = 0.1

-- 財務影響の正規化
total_financial_impact = margin_erosion_jpy + pipeline_at_risk_jpy
financial_ratio = MIN(1.0, total_financial_impact / (avg_project_value_jpy × active_projects_count × 0.1))

raw_score = (severity_weight × max_component_criticality × 0.25
           + schedule_weight × 0.25
           + financial_ratio × 0.25
           + relationship_weight × 0.10
           + effective_vulnerability × 0.15) × 100

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
| `affected_project_count` | int | 影響を受けるプロジェクト数 |
| `effective_delay_weeks` | int | 実効遅延期間（バッファ考慮後） |
| `penalty_exposure_jpy` | int | ペナルティリスク額（円） |
| `margin_erosion_jpy` | int | 利益率低下額（円） |
| `pipeline_at_risk_jpy` | int | リスクにさらされるパイプライン額（円） |
| `schedule_risk_level` | enum | 納期リスクレベル |
| `recommended_actions` | list[string] | 推奨アクションリスト |
| `risk_summary` | string | リスクサマリー文（自然言語） |

### 推奨アクション分岐

| レベル | 推奨アクション |
|---|---|
| critical | 全HW調達案件の緊急棚卸しと顧客への早期影響通知、遅延ペナルティ免責交渉の即時開始（不可抗力条項の援用検討）、クラウドファースト提案への緊急切替（オンプレミス→IaaS/PaaS）、代替ベンダーの緊急認定プロセス発動、戦略的バッファ在庫の積み増し発注、経営層へのプロジェクトポートフォリオリスク報告 |
| high | 調達フェーズプロジェクトのスケジュール再調整、顧客との納期見直し協議の開始、クラウド代替提案の積極推進、主要ベンダーとの優先供給枠確保交渉、パイプライン案件のHW依存度再評価 |
| medium | 調達リードタイムの見積もり前提条件の更新、代替機器の技術検証開始、受注前HW調達リスクアセスメントの強化、プロジェクト計画へのバッファ期間追加 |
| low | サプライチェーン動向のモニタリング継続、四半期レビューでの報告、長期的なHW依存低減戦略の検討 |

## 参照データソース（Fabric テーブル）

| テーブル名 | 説明 | 主要カラム |
|---|---|---|
| `fact_project_milestones` | プロジェクトマイルストーン実績 | `project_id`, `milestone_name`, `planned_date`, `actual_date`, `status`, `delay_days` |
| `fact_procurement_orders` | 機器調達発注実績 | `order_id`, `project_id`, `vendor_id`, `component_type`, `quantity`, `order_date`, `estimated_delivery`, `actual_delivery`, `status` |
| `dim_projects` | プロジェクトマスタ | `project_id`, `customer_id`, `project_name`, `contract_value_jpy`, `hw_cost_ratio`, `start_date`, `end_date`, `penalty_clause`, `penalty_per_week_jpy` |
| `dim_vendors` | ベンダーマスタ | `vendor_id`, `vendor_name`, `component_types`, `lead_time_weeks`, `country`, `tier`, `alternative_vendors` |
| `fact_inventory_buffer` | 戦略在庫スナップショット | `snapshot_date`, `component_type`, `quantity`, `weeks_of_supply`, `warehouse_id` |
| `fact_pipeline_opportunities` | 案件パイプライン | `opportunity_id`, `customer_id`, `estimated_value_jpy`, `probability`, `hw_dependency`, `expected_close_date`, `status` |
| `fact_project_financials` | プロジェクト収支実績 | `project_id`, `month`, `revenue_jpy`, `cost_jpy`, `hw_cost_jpy`, `margin_rate`, `variance_jpy` |
| `agg_delivery_performance` | 納期遵守率集計 | `quarter`, `on_time_ratio`, `avg_delay_days`, `penalty_total_jpy`, `customer_satisfaction_score` |
