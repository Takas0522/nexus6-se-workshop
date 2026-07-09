# 携帯電話事業：通信品質基準厳格化インパクト判断スキル

## 概要

総務省による通信品質基準・冗長性要件の厳格化が携帯電話事業に与えるインパクトを判断するスキル。大規模通信障害を契機とした規制強化に対し、現行ネットワーク設備の準拠ギャップ、追加設備投資額、運用コスト増大、およびMVNO接続料への影響を定量的に評価し、推奨アクションを出力する。

## 入力パラメータ

### ニュース情報

| パラメータ | 型 | 説明 |
|---|---|---|
| `news_category` | string | ニュースカテゴリ（`infrastructure_regulation`） |
| `regulation_authority` | string | 規制当局（例: `総務省`, `電気通信紛争処理委員会`） |
| `new_availability_target` | float | 新基準の稼働率目標（例: `0.999999` = シックスナイン） |
| `new_redundancy_requirement` | string | 新冗長性要件（例: `N+2`, `geo_redundant`） |
| `max_recovery_time_minutes` | int | 新基準の障害復旧時間上限（分） |
| `enforcement_date` | date | 施行予定日 |
| `transition_period_months` | int | 経過措置期間（月） |
| `applies_to_mvno` | bool | MVNO事業者への適用有無 |

### 業務データ

| パラメータ | 型 | 説明 |
|---|---|---|
| `current_availability` | float | 現行ネットワーク稼働率（例: `0.99999`） |
| `current_redundancy_level` | string | 現行冗長構成（例: `N+1`, `N+2`） |
| `current_recovery_time_minutes` | int | 現行平均復旧時間（分） |
| `annual_capex_budget_jpy` | int | 年間設備投資予算（円） |
| `base_station_count` | int | 基地局数 |
| `backup_power_hours` | int | バックアップ電源稼働時間（時間） |
| `network_segments_count` | int | ネットワークセグメント数 |
| `non_compliant_segment_ratio` | float | 新基準未準拠セグメント比率（0.0〜1.0） |
| `mvno_wholesale_revenue_jpy` | int | MVNO卸売年間収益（円） |
| `annual_opex_network_jpy` | int | ネットワーク年間運用費（円） |

## 判断ロジック

### Step 1: 準拠ギャップの評価

```
-- 稼働率ギャップ
availability_gap = new_availability_target - current_availability
IF availability_gap > 0.00009 THEN availability_compliance = "critical_gap"   -- 99.999% → 99.9999%
ELSE IF availability_gap > 0.0001 THEN availability_compliance = "major_gap"
ELSE IF availability_gap > 0 THEN availability_compliance = "minor_gap"
ELSE availability_compliance = "compliant"

-- 冗長性ギャップ
redundancy_map = {"N+0": 0, "N+1": 1, "N+2": 2, "N+3": 3}
required_level = redundancy_map[new_redundancy_requirement]
current_level = redundancy_map[current_redundancy_level]
redundancy_gap = required_level - current_level

-- 復旧時間ギャップ
recovery_gap = current_recovery_time_minutes - max_recovery_time_minutes
IF recovery_gap > 20 THEN recovery_compliance = "critical_gap"
ELSE IF recovery_gap > 5 THEN recovery_compliance = "major_gap"
ELSE IF recovery_gap > 0 THEN recovery_compliance = "minor_gap"
ELSE recovery_compliance = "compliant"
```

### Step 2: 追加設備投資額の推定

```
-- 基地局あたりの冗長化コスト
per_station_redundancy_cost_jpy = redundancy_gap × 8_000_000  -- 1段階あたり約800万円/局

-- バックアップ電源強化コスト（72時間対応）
IF backup_power_hours < 72 THEN
    power_upgrade_cost_jpy = base_station_count × (72 - backup_power_hours) × 150_000
ELSE
    power_upgrade_cost_jpy = 0

-- 未準拠セグメントの改修コスト
segment_upgrade_cost_jpy = network_segments_count × non_compliant_segment_ratio × 500_000_000

-- AIOps/自動復旧システム導入コスト（復旧時間短縮用）
IF recovery_compliance IN ("critical_gap", "major_gap") THEN
    aiops_cost_jpy = 3_000_000_000  -- 30億円
ELSE IF recovery_compliance == "minor_gap" THEN
    aiops_cost_jpy = 1_000_000_000
ELSE
    aiops_cost_jpy = 0

total_additional_capex_jpy = (per_station_redundancy_cost_jpy × base_station_count × non_compliant_segment_ratio)
                           + power_upgrade_cost_jpy + segment_upgrade_cost_jpy + aiops_cost_jpy
```

### Step 3: 運用コスト増大の推定

```
-- 冗長構成による年間運用費増加（設備増に伴う保守・電力・回線費）
opex_increase_ratio = redundancy_gap × 0.08 + (non_compliant_segment_ratio × 0.05)
annual_opex_increase_jpy = annual_opex_network_jpy × opex_increase_ratio

-- コンプライアンス報告体制コスト（定期報告義務）
compliance_reporting_cost_jpy = 200_000_000  -- 年間2億円（人件費+システム）
```

### Step 4: 財務インパクト評価

```
total_3year_cost_jpy = total_additional_capex_jpy + (annual_opex_increase_jpy + compliance_reporting_cost_jpy) × 3
capex_budget_pressure = total_additional_capex_jpy / annual_capex_budget_jpy

IF capex_budget_pressure >= 0.5 THEN financial_severity = "critical"  -- 年間予算の50%超
ELSE IF capex_budget_pressure >= 0.25 THEN financial_severity = "high"
ELSE IF capex_budget_pressure >= 0.1 THEN financial_severity = "medium"
ELSE financial_severity = "low"
```

### Step 5: 時間的切迫度の評価

```
months_to_compliance = transition_period_months
estimated_implementation_months = (non_compliant_segment_ratio × 36) + (redundancy_gap × 12)

time_margin = months_to_compliance - estimated_implementation_months

IF time_margin < 0 THEN time_pressure = "critical"     -- 期限内完了不可能
ELSE IF time_margin < 6 THEN time_pressure = "high"
ELSE IF time_margin < 12 THEN time_pressure = "medium"
ELSE time_pressure = "low"
```

### Step 6: 総合インパクトスコア算出

```
gap_severity_weight:
  critical_gap = 1.0
  major_gap = 0.7
  minor_gap = 0.35
  compliant = 0.0

-- 最大のギャップを代表値とする
max_gap_weight = MAX(gap_weight(availability_compliance), gap_weight(recovery_compliance), redundancy_gap / 2)

financial_weight:
  critical = 1.0
  high = 0.75
  medium = 0.45
  low = 0.15

time_weight:
  critical = 1.0
  high = 0.7
  medium = 0.4
  low = 0.15

raw_score = (max_gap_weight × 0.30 + financial_weight × 0.35 + time_weight × 0.25 + non_compliant_segment_ratio × 0.10) × 100
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
| `compliance_gap_summary` | object | 稼働率・冗長性・復旧時間の各ギャップ |
| `additional_capex_estimate_jpy` | int | 追加設備投資推定額（円） |
| `annual_opex_increase_jpy` | int | 年間運用費増加推定額（円） |
| `capex_budget_pressure_ratio` | float | 予算圧迫率 |
| `time_pressure_level` | enum | 時間的切迫度 |
| `recommended_actions` | list[string] | 推奨アクションリスト |
| `risk_summary` | string | リスクサマリー文（自然言語） |

### 推奨アクション分岐

| レベル | 推奨アクション |
|---|---|
| critical | 中期設備投資計画の緊急全面見直し、コアネットワーク冗長化プロジェクトの即時立ち上げ（N+2移行）、AIOps障害検知・自動復旧システムの導入決定、バックアップ電源72時間対応への増強着手、総務省への対応計画提出準備、経営層への投資承認エスカレーション |
| high | 優先セグメントの冗長化計画策定、障害復旧プロセスの自動化推進、予備電源設備の段階的増強、MVNO接続料改定の検討開始、コンプライアンス報告体制の構築 |
| medium | 準拠ギャップの詳細アセスメント実施、投資優先順位の再評価、既存設備の延命/更改判断、業界団体を通じた規制当局との対話 |
| low | 規制動向のモニタリング継続、次期投資計画への反映検討、定期報告への記載 |

## 参照データソース（Fabric テーブル）

| テーブル名 | 説明 | 主要カラム |
|---|---|---|
| `fact_network_availability` | ネットワーク稼働率実績 | `date`, `segment_id`, `uptime_seconds`, `downtime_seconds`, `availability_rate` |
| `fact_network_incidents` | ネットワーク障害履歴 | `incident_id`, `start_time`, `end_time`, `affected_users`, `severity`, `root_cause`, `recovery_minutes` |
| `dim_network_segments` | ネットワークセグメントマスタ | `segment_id`, `segment_type`, `redundancy_level`, `region`, `capacity`, `commissioned_date` |
| `dim_base_stations` | 基地局マスタ | `station_id`, `location`, `generation`, `backup_power_hours`, `last_maintenance_date` |
| `fact_capex_actuals` | 設備投資実績 | `fiscal_year`, `investment_category`, `amount_jpy`, `segment_id`, `project_id` |
| `fact_opex_network` | ネットワーク運用費実績 | `month`, `cost_category`, `amount_jpy`, `segment_id` |
| `dim_compliance_requirements` | コンプライアンス要件マスタ | `requirement_id`, `authority`, `requirement_type`, `target_value`, `effective_date`, `status` |
| `agg_quarterly_sla_performance` | 四半期SLA実績集計 | `quarter`, `segment_id`, `availability_achieved`, `incidents_count`, `avg_recovery_minutes`, `sla_breach_count` |
