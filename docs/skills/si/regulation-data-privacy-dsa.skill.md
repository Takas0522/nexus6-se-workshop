# SI事業：EU DSA個人データ規制強化インパクト判断スキル

## 概要

EUデジタルサービス法（DSA）等の個人データ規制強化がSI事業に与えるインパクトを判断するスキル。規制対応に伴うシステム改修需要（ビジネス機会）と、受託開発における個人データ処理責任の拡大（リスク）の両面を評価し、案件パイプラインへの影響度、既存契約の見直し必要性、および新規サービス化の優先度を出力する。

## 入力パラメータ

### ニュース情報

| パラメータ | 型 | 説明 |
|---|---|---|
| `news_category` | string | ニュースカテゴリ（`regulation_compliance`） |
| `regulation_name` | string | 規制名称（例: `EU_DSA`, `GDPR`, `個人情報保護法改正`） |
| `regulation_scope` | enum | `extraterritorial` / `domestic` / `sector_specific` |
| `penalty_rate_percent` | float | 制裁金率（売上高に対する%） |
| `enforcement_timeline_months` | int | 施行までの猶予期間（月） |
| `affected_industries` | list[string] | 影響を受ける業界（例: `platform`, `advertising`, `financial`, `healthcare`） |
| `compliance_requirements` | list[string] | 準拠要件（例: `consent_management`, `data_mapping`, `anonymization`, `audit_trail`, `dpia`） |

### 業務データ

| パラメータ | 型 | 説明 |
|---|---|---|
| `active_projects_count` | int | 進行中プロジェクト数 |
| `data_handling_project_ratio` | float | 個人データ処理を含むプロジェクト比率（0.0〜1.0） |
| `platform_customer_count` | int | プラットフォーム/SNS系顧客数 |
| `total_customer_count` | int | SI事業全顧客数 |
| `privacy_certified_engineers` | int | プライバシー関連有資格エンジニア数 |
| `total_engineers` | int | エンジニア総数 |
| `existing_privacy_solutions` | list[string] | 自社パッケージ/ソリューション（例: `CMP`, `DMP_anonymizer`, `consent_SDK`） |
| `avg_compliance_project_value_jpy` | int | コンプライアンス案件の平均単価（円） |
| `current_pipeline_privacy_jpy` | int | プライバシー関連パイプライン額（円） |
| `contracts_with_data_processor_clause` | int | データ処理者条項付き契約数 |
| `joint_controller_risk_contracts` | int | 共同管理者リスクのある契約数 |

## 判断ロジック

### Step 1: ビジネス機会の評価

```
-- 規制対応需要の市場規模推定
affected_customer_ratio = platform_customer_count / total_customer_count
requirement_complexity = COUNT(compliance_requirements)

-- 需要の緊急度
IF enforcement_timeline_months <= 12 THEN demand_urgency = "immediate"
ELSE IF enforcement_timeline_months <= 24 THEN demand_urgency = "near_term"
ELSE demand_urgency = "long_term"

-- 新規案件創出ポテンシャル
potential_new_projects = platform_customer_count × requirement_complexity × 0.3
estimated_opportunity_jpy = potential_new_projects × avg_compliance_project_value_jpy

-- 既存パイプラインの加速効果
IF demand_urgency == "immediate" THEN pipeline_acceleration = 1.5
ELSE IF demand_urgency == "near_term" THEN pipeline_acceleration = 1.2
ELSE pipeline_acceleration = 1.0

accelerated_pipeline_jpy = current_pipeline_privacy_jpy × pipeline_acceleration
```

### Step 2: 自社ケイパビリティの評価

```
-- プライバシー人材の充足度
privacy_engineer_ratio = privacy_certified_engineers / total_engineers
IF privacy_engineer_ratio >= 0.1 THEN capability_level = "strong"
ELSE IF privacy_engineer_ratio >= 0.05 THEN capability_level = "adequate"
ELSE IF privacy_engineer_ratio >= 0.02 THEN capability_level = "developing"
ELSE capability_level = "insufficient"

-- ソリューション整備度
solution_coverage = COUNT(existing_privacy_solutions ∩ compliance_requirements) / COUNT(compliance_requirements)

-- 機会獲得力スコア
capability_score:
  strong = 0.9
  adequate = 0.65
  developing = 0.35
  insufficient = 0.1

opportunity_capture_rate = capability_score × (0.7 + solution_coverage × 0.3)
realizable_opportunity_jpy = estimated_opportunity_jpy × opportunity_capture_rate
```

### Step 3: 既存契約リスクの評価

```
-- データ処理者としての責任拡大リスク
processor_risk_exposure = contracts_with_data_processor_clause × avg_compliance_project_value_jpy × 0.05
-- 契約額の5%が追加対応コストとして発生

-- 共同管理者認定リスク（制裁金の連帯責任）
IF joint_controller_risk_contracts > 0 THEN
    joint_controller_penalty_risk = joint_controller_risk_contracts × (penalty_rate_percent / 100) × avg_compliance_project_value_jpy × 10
    -- 顧客売上高の代理としてPJ額×10倍を使用
ELSE
    joint_controller_penalty_risk = 0

-- 契約見直しの緊急度
contracts_requiring_review = contracts_with_data_processor_clause + joint_controller_risk_contracts
IF contracts_requiring_review >= 20 THEN contract_risk_level = "high"
ELSE IF contracts_requiring_review >= 5 THEN contract_risk_level = "medium"
ELSE contract_risk_level = "low"
```

### Step 4: 対応コストの算出

```
-- 人材獲得・育成コスト
target_privacy_ratio = 0.08  -- 目標8%
additional_engineers_needed = MAX(0, (total_engineers × target_privacy_ratio) - privacy_certified_engineers)
hiring_training_cost_jpy = additional_engineers_needed × 3_000_000  -- 採用・育成1人あたり300万円

-- ソリューション開発コスト
missing_solutions = compliance_requirements - existing_privacy_solutions
solution_development_cost_jpy = COUNT(missing_solutions) × 200_000_000  -- 1ソリューションあたり2億円

-- 既存契約改修コスト（自社負担分）
contract_remediation_cost_jpy = contracts_requiring_review × 5_000_000  -- 契約あたり500万円

total_investment_required_jpy = hiring_training_cost_jpy + solution_development_cost_jpy + contract_remediation_cost_jpy
```

### Step 5: ネットインパクト（機会 vs リスク）の算出

```
-- 3年間の機会価値
three_year_opportunity_jpy = realizable_opportunity_jpy × 3 + accelerated_pipeline_jpy

-- 3年間のリスク・コスト
three_year_risk_cost_jpy = processor_risk_exposure + joint_controller_penalty_risk + total_investment_required_jpy

net_impact_jpy = three_year_opportunity_jpy - three_year_risk_cost_jpy
-- 正値 = 機会優位、負値 = リスク優位

IF net_impact_jpy > 0 THEN impact_nature = "opportunity_dominant"
ELSE impact_nature = "risk_dominant"
```

### Step 6: 総合インパクトスコア算出

```
-- 機会側スコア（高いほど大きな機会）
opportunity_magnitude = MIN(1.0, realizable_opportunity_jpy / (avg_compliance_project_value_jpy × 50))

-- リスク側スコア（高いほど大きなリスク）
risk_magnitude = MIN(1.0, three_year_risk_cost_jpy / (avg_compliance_project_value_jpy × 30))

-- 対応切迫度
urgency_weight:
  immediate = 1.0
  near_term = 0.6
  long_term = 0.3

-- ケイパビリティギャップ（不足しているほどスコア高 = アクション必要）
capability_gap = 1.0 - opportunity_capture_rate

raw_score = (MAX(opportunity_magnitude, risk_magnitude) × 0.35
           + urgency_weight × 0.25
           + capability_gap × 0.20
           + (contracts_requiring_review / MAX(active_projects_count, 1)) × 0.20) × 100

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
| `estimated_opportunity_jpy` | int | 新規案件創出ポテンシャル（円） |
| `realizable_opportunity_jpy` | int | 実現可能な機会額（円） |
| `total_risk_exposure_jpy` | int | リスク総額（円） |
| `capability_gap_level` | enum | ケイパビリティギャップレベル |
| `contracts_requiring_review` | int | 見直しが必要な契約数 |
| `recommended_actions` | list[string] | 推奨アクションリスト |
| `risk_summary` | string | リスクサマリー文（自然言語） |

### 推奨アクション分岐

| レベル | 推奨アクション |
|---|---|
| critical | DSA/プライバシー対応専門チームの緊急設置（10名以上）、顧客向けDSA準拠アセスメントサービスの即時ローンチ、CMP/データガバナンスソリューションの自社パッケージ化決定、共同管理者リスク契約の法務レビュー緊急実施、GDPR/DSA有資格者の大量採用開始、規制対応コンサルティングメニューの策定・価格設定 |
| high | プライバシーエンジニアの計画的採用・育成（年間15名増）、既存顧客への規制影響レター発送とアップセル活動、ソリューション不足領域のパートナーシップ締結、受託契約テンプレートのデータ処理条項改定 |
| medium | プライバシー関連認定資格の取得推進、市場動向調査と競合ソリューション分析、パイロット顧客でのDSA対応案件の実績づくり、社内ナレッジベースの整備 |
| low | 規制動向のモニタリング継続、業界セミナーへの参加・情報収集、中長期の事業計画への反映検討 |

## 参照データソース（Fabric テーブル）

| テーブル名 | 説明 | 主要カラム |
|---|---|---|
| `dim_projects` | プロジェクトマスタ | `project_id`, `customer_id`, `project_type`, `involves_personal_data`, `data_processor_role`, `contract_value_jpy` |
| `dim_customers` | 顧客マスタ | `customer_id`, `company_name`, `industry`, `is_platform_operator`, `eu_presence`, `annual_revenue_jpy` |
| `fact_pipeline_opportunities` | 案件パイプライン | `opportunity_id`, `customer_id`, `solution_area`, `estimated_value_jpy`, `probability`, `expected_close_date` |
| `dim_engineers` | エンジニアマスタ | `engineer_id`, `skill_area`, `certifications`, `privacy_qualified`, `experience_years` |
| `dim_solutions` | ソリューションカタログ | `solution_id`, `solution_name`, `category`, `target_regulation`, `maturity_level`, `annual_revenue_jpy` |
| `fact_contract_clauses` | 契約条項管理 | `contract_id`, `project_id`, `clause_type`, `data_role`, `liability_cap_jpy`, `review_status` |
| `fact_compliance_projects` | コンプライアンス案件実績 | `project_id`, `regulation_name`, `scope`, `deliverables`, `revenue_jpy`, `completion_date` |
| `agg_privacy_market_demand` | プライバシー市場需要集計 | `quarter`, `regulation_trigger`, `inquiry_count`, `proposal_count`, `won_count`, `total_value_jpy` |
