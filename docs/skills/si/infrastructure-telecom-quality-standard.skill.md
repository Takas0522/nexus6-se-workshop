# SI事業：通信品質基準厳格化インパクト判断スキル

## 概要

総務省による通信品質基準・冗長性要件の厳格化がSI事業に与えるインパクトを判断するスキル。通信事業者向けシステム構築需要の増加（ビジネス機会）と、自社が運用受託するシステムのSLA見直し要求（リスク）の両面を評価し、案件パイプラインへの正負の影響、要員確保の必要性、および既存運用契約の見直し優先度を出力する。

## 入力パラメータ

### ニュース情報

| パラメータ | 型 | 説明 |
|---|---|---|
| `news_category` | string | ニュースカテゴリ（`infrastructure_regulation`） |
| `regulation_authority` | string | 規制当局（例: `総務省`） |
| `new_availability_target` | float | 新基準の稼働率目標（例: `0.999999`） |
| `new_redundancy_requirement` | string | 新冗長性要件（例: `N+2`, `geo_redundant`） |
| `max_recovery_time_minutes` | int | 新基準の障害復旧時間上限（分） |
| `enforcement_timeline_months` | int | 施行までの猶予期間（月） |
| `affected_operator_types` | list[string] | 対象事業者種別（例: `MNO`, `MVNO`, `ISP`, `cloud_provider`） |
| `estimated_industry_capex_increase_percent` | float | 業界全体の設備投資増加見込み（%） |

### 業務データ

| パラメータ | 型 | 説明 |
|---|---|---|
| `telecom_customer_count` | int | 通信事業者系顧客数 |
| `total_customer_count` | int | SI事業全顧客数 |
| `telecom_revenue_ratio` | float | 通信事業者向け売上比率（0.0〜1.0） |
| `infra_project_active_count` | int | インフラ系進行中プロジェクト数 |
| `managed_service_contracts` | int | 運用保守契約数 |
| `managed_service_with_sla_count` | int | SLA付き運用保守契約数 |
| `current_avg_sla_availability` | float | 現行SLA平均稼働率保証値 |
| `network_engineer_count` | int | ネットワーク/インフラエンジニア数 |
| `total_engineers` | int | エンジニア総数 |
| `avg_infra_project_value_jpy` | int | インフラ案件平均契約額（円） |
| `current_pipeline_infra_jpy` | int | インフラ関連パイプライン額（円） |
| `annual_managed_service_revenue_jpy` | int | 年間運用保守収益（円） |

## 判断ロジック

### Step 1: ビジネス機会の評価

```
-- 通信品質基準厳格化に伴うシステム構築需要の推定
demand_multiplier:
  enforcement_timeline_months <= 12 = 2.5   -- 緊急対応で需要急増
  enforcement_timeline_months <= 24 = 1.8
  enforcement_timeline_months <= 36 = 1.3
  enforcement_timeline_months > 36 = 1.1

-- 新規案件創出ポテンシャル
new_project_types = [
    "ネットワーク冗長化設計・構築",
    "AIOps/障害自動復旧システム",
    "監視基盤刷新",
    "BCP/DR環境構築",
    "コンプライアンス報告システム"
]
potential_projects_per_customer = COUNT(new_project_types) × 0.6  -- 顧客あたり平均3案件

estimated_new_opportunity_jpy = telecom_customer_count × potential_projects_per_customer × avg_infra_project_value_jpy × demand_multiplier
pipeline_uplift_jpy = current_pipeline_infra_jpy × (demand_multiplier - 1.0)
```

### Step 2: 自社ケイパビリティの評価

```
-- インフラエンジニアの充足度
infra_engineer_ratio = network_engineer_count / total_engineers

IF infra_engineer_ratio >= 0.15 THEN resource_readiness = "strong"
ELSE IF infra_engineer_ratio >= 0.10 THEN resource_readiness = "adequate"
ELSE IF infra_engineer_ratio >= 0.05 THEN resource_readiness = "stretched"
ELSE resource_readiness = "insufficient"

-- 需要に対するキャパシティ
estimated_required_engineers = (estimated_new_opportunity_jpy / avg_infra_project_value_jpy) × 5  -- PJあたり5名
capacity_gap = MAX(0, estimated_required_engineers - network_engineer_count)

IF capacity_gap / MAX(network_engineer_count, 1) >= 1.0 THEN capacity_constraint = "critical"
ELSE IF capacity_gap / MAX(network_engineer_count, 1) >= 0.5 THEN capacity_constraint = "high"
ELSE IF capacity_gap > 0 THEN capacity_constraint = "moderate"
ELSE capacity_constraint = "none"
```

### Step 3: 既存運用契約のSLAリスク評価

```
-- SLA引き上げ要求の見込み
sla_gap = new_availability_target - current_avg_sla_availability

IF sla_gap > 0.00009 THEN sla_upgrade_pressure = "critical"   -- 99.999% → 99.9999%
ELSE IF sla_gap > 0.0001 THEN sla_upgrade_pressure = "high"
ELSE IF sla_gap > 0 THEN sla_upgrade_pressure = "moderate"
ELSE sla_upgrade_pressure = "none"

-- SLA引き上げに伴う運用コスト増大
sla_cost_increase_ratio:
  critical = 0.40   -- 運用コスト40%増
  high = 0.25
  moderate = 0.12
  none = 0.0

annual_sla_cost_increase_jpy = annual_managed_service_revenue_jpy × sla_cost_increase_ratio × 0.7
-- 収益の70%がコストと仮定、そこに増加率を適用

-- 契約見直し対象数
contracts_requiring_sla_review = managed_service_with_sla_count
IF sla_upgrade_pressure IN ("critical", "high") THEN
    contracts_at_loss_risk = managed_service_with_sla_count × 0.3  -- 30%が採算悪化
ELSE
    contracts_at_loss_risk = managed_service_with_sla_count × 0.1
```

### Step 4: ネットインパクト（機会 vs リスク）の算出

```
-- 3年間の機会価値（獲得率を加味）
opportunity_capture_rate:
  strong = 0.7
  adequate = 0.5
  stretched = 0.3
  insufficient = 0.1

three_year_opportunity_jpy = estimated_new_opportunity_jpy × opportunity_capture_rate + pipeline_uplift_jpy

-- 3年間のリスク・投資コスト
hiring_cost_jpy = capacity_gap × 5_000_000  -- 1名あたり500万円（採用+オンボーディング）
sla_upgrade_investment_jpy = annual_sla_cost_increase_jpy × 3
three_year_risk_cost_jpy = hiring_cost_jpy + sla_upgrade_investment_jpy

net_impact_jpy = three_year_opportunity_jpy - three_year_risk_cost_jpy
IF net_impact_jpy > 0 THEN impact_nature = "opportunity_dominant"
ELSE impact_nature = "risk_dominant"
```

### Step 5: 時間的切迫度

```
-- 体制構築に必要な期間
ramp_up_months = (capacity_gap / MAX(network_engineer_count × 0.2, 1)) × 6  -- 月間採用可能数の20%
time_margin = enforcement_timeline_months - ramp_up_months

IF time_margin < 0 THEN time_pressure = "critical"
ELSE IF time_margin < 6 THEN time_pressure = "high"
ELSE IF time_margin < 12 THEN time_pressure = "medium"
ELSE time_pressure = "low"

time_weight:
  critical = 1.0
  high = 0.7
  medium = 0.4
  low = 0.2
```

### Step 6: 総合インパクトスコア算出

```
-- 機会規模の正規化
opportunity_scale = MIN(1.0, estimated_new_opportunity_jpy / (avg_infra_project_value_jpy × 100))

-- SLAリスクの正規化
sla_risk_weight:
  critical = 1.0
  high = 0.7
  moderate = 0.35
  none = 0.0

-- キャパシティ制約の正規化
capacity_weight:
  critical = 0.9
  high = 0.6
  moderate = 0.3
  none = 0.0

raw_score = (opportunity_scale × 0.30
           + sla_risk_weight × 0.25
           + time_weight × 0.20
           + capacity_weight × 0.15
           + telecom_revenue_ratio × 0.10) × 100

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
| `impact_nature` | enum | `opportunity_dominant` / `risk_dominant` |
| `estimated_opportunity_jpy` | int | 新規案件機会額（円） |
| `pipeline_uplift_jpy` | int | パイプライン増分（円） |
| `capacity_gap_engineers` | int | エンジニア不足数 |
| `contracts_requiring_sla_review` | int | SLA見直し対象契約数 |
| `annual_sla_cost_increase_jpy` | int | SLA対応年間コスト増（円） |
| `time_pressure_level` | enum | 時間的切迫度 |
| `recommended_actions` | list[string] | 推奨アクションリスト |
| `risk_summary` | string | リスクサマリー文（自然言語） |

### 推奨アクション分岐

| レベル | 推奨アクション |
|---|---|
| critical | ネットワーク/インフラエンジニアの大量採用開始（目標+30名/四半期）、通信事業者向け「品質基準準拠パッケージ」の緊急策定・提案、AIOps/自動復旧ソリューションの自社開発またはパートナー連携決定、既存SLA付き契約の採算再評価と価格改定交渉開始、経営層への事業ポートフォリオ変更提案 |
| high | インフラ系エンジニアの計画的増員（年間20名）、通信事業者の設備投資計画ヒアリングと先行提案、運用保守契約のSLA条項・価格体系の改定準備、パートナー企業との協業体制構築 |
| medium | 通信品質基準の技術要件の詳細分析、対応可能ソリューションの棚卸しと不足領域の特定、既存顧客への情報提供と意向確認、採用計画への中期的な反映 |
| low | 規制動向のモニタリング継続、業界セミナー・展示会での情報収集、中長期事業計画への検討事項として記録 |

## 参照データソース（Fabric テーブル）

| テーブル名 | 説明 | 主要カラム |
|---|---|---|
| `dim_customers` | 顧客マスタ | `customer_id`, `company_name`, `industry`, `is_telecom_operator`, `operator_type`, `annual_it_budget_jpy` |
| `dim_projects` | プロジェクトマスタ | `project_id`, `customer_id`, `project_type`, `category`, `contract_value_jpy`, `start_date`, `end_date` |
| `fact_managed_services` | 運用保守契約実績 | `contract_id`, `customer_id`, `service_type`, `sla_availability_target`, `monthly_fee_jpy`, `penalty_clause`, `expiry_date` |
| `fact_pipeline_opportunities` | 案件パイプライン | `opportunity_id`, `customer_id`, `solution_area`, `estimated_value_jpy`, `probability`, `expected_close_date`, `is_infra_related` |
| `dim_engineers` | エンジニアマスタ | `engineer_id`, `skill_area`, `certifications`, `specialization`, `utilization_rate`, `availability_date` |
| `fact_project_delivery` | プロジェクト納品実績 | `project_id`, `deliverable_type`, `planned_date`, `actual_date`, `quality_score`, `customer_satisfaction` |
| `fact_sla_performance` | SLA実績 | `contract_id`, `month`, `availability_achieved`, `incidents_count`, `breach_count`, `penalty_incurred_jpy` |
| `agg_telecom_sector_revenue` | 通信セクター収益集計 | `quarter`, `customer_segment`, `revenue_jpy`, `project_count`, `avg_deal_size_jpy`, `growth_rate` |
