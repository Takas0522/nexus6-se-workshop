# SI事業：半導体供給制約インパクト判断スキル

## 前提

- 本文書は nexus6-se-workshop の Demo 用ナレッジであり、数値・事例はすべて想定値である。
- 個人情報、実在企業の機密、実顧客の行動履歴は含めない。
- 対象事業部は SI事業 である。
- 対応シナリオは「世界的半導体供給制約の深刻化（サプライチェーンリスク）」。
- Agent 2 は影響有無の評価、Agent 3 は事業部別 Next Action の草案作成に使う。

## 概要

米中間の半導体輸出規制強化に伴うサーバー・ネットワーク機器の調達遅延が、SI事業の進行中プロジェクトの納期遵守、新規案件の受注判断、ハードウェア調達コスト増によるプロジェクト採算性、および顧客との契約条件（納期ペナルティ・瑕疵担保）に与えるインパクトを判断するスキル。影響プロジェクト数、遅延リスク金額、エンジニアリソースの遊休化リスクを定量評価し、推奨アクションを出力する。

## 適用シナリオ

- 外部ニュースのトリガー: 半導体輸出規制の報道、サーバーベンダーの納期延長通知、ネットワーク機器メーカーの出荷制限発表、チップメーカーの供給見通し下方修正。
- SI事業はハードウェア調達を含むインフラ構築プロジェクトが多く、サーバー・ネットワーク機器の納品遅延がプロジェクト全体のクリティカルパスに直結する。
- 機器調達が完了しないとテスト・移行フェーズに進めず、エンジニアの待機（遊休）コストが発生する。

## 入力パラメータ

### ニュース情報

| パラメータ | 型 | 説明 |
|---|---|---|
| `news_category` | string | ニュースカテゴリ（`supply_chain`） |
| `affected_equipment_types` | list[string] | 影響を受ける機器種別（例: `server`, `network_switch`, `storage`, `gpu_server`, `firewall`） |
| `supply_delay_months` | int | 供給遅延期間（月数、例: 3〜6） |
| `affected_manufacturers` | list[string] | 影響を受けるメーカー名 |
| `regulation_scope` | string | 規制範囲（例: `export_ban`, `license_required`, `entity_list`） |
| `price_increase_pct` | float | 調達コスト上昇率（%） |
| `secondary_source_available` | bool | 代替調達先の有無 |

### 業務データ

| パラメータ | 型 | 説明 | 参照テーブル |
|---|---|---|---|
| `active_projects_count` | int | 進行中プロジェクト数 | `si_projects` (status=IN_PROGRESS) |
| `projects_with_hw_procurement` | int | HW調達を含むプロジェクト数 | `si_projects` 分析 |
| `projects_in_procurement_phase` | int | 現在調達フェーズにあるプロジェクト数 | `si_projects` 分析 |
| `total_hw_procurement_budget_jpy` | int | HW調達予算合計（円） | `si_contracts` 集計 |
| `avg_project_contract_amount_jpy` | int | プロジェクト平均契約金額（円） | `si_contracts` 集計 |
| `projects_with_penalty_clause` | int | 納期遅延ペナルティ条項付きプロジェクト数 | `si_contracts` 分析 |
| `avg_penalty_rate_pct` | float | 平均ペナルティ率（契約金額比%/月） | `si_contracts` 分析 |
| `total_assigned_engineers` | int | HW調達プロジェクトへのアサイン済みエンジニア数 | `si_engineers` 集計 |
| `avg_engineer_hourly_rate_jpy` | int | エンジニア平均時間単価（円） | `si_engineers` 集計 |
| `vendor_contracts_count` | int | 機器ベンダーとの契約数 | 調達管理 |
| `affected_vendor_ratio` | float | 影響を受けるベンダーの比率（0.0〜1.0） | 調達管理 |
| `backlog_pipeline_jpy` | int | 受注残・パイプライン金額（円） | `si_contracts` + 営業管理 |

## 判断ロジック

### Step 1: 影響プロジェクトの特定

```
-- 影響を受けるプロジェクトの推定
at_risk_projects = projects_with_hw_procurement × affected_vendor_ratio

-- フェーズ別の影響度
IF projects_in_procurement_phase > 0 THEN
  immediate_risk_projects = projects_in_procurement_phase × affected_vendor_ratio
ELSE
  immediate_risk_projects = 0

-- 今後 supply_delay_months 以内に調達フェーズに入るプロジェクト
upcoming_procurement_projects = (projects_with_hw_procurement - projects_in_procurement_phase) × affected_vendor_ratio × MIN(1.0, supply_delay_months / 6)

total_at_risk = immediate_risk_projects + upcoming_procurement_projects
project_risk_ratio = total_at_risk / MAX(1, active_projects_count)
```

### Step 2: 納期遅延リスクの評価

```
-- 遅延可能性の判定
IF supply_delay_months >= 6 AND affected_vendor_ratio >= 0.5 THEN
  delay_severity = "critical"
  estimated_delay_months = supply_delay_months × 0.8
ELSE IF supply_delay_months >= 4 OR affected_vendor_ratio >= 0.7 THEN
  delay_severity = "high"
  estimated_delay_months = supply_delay_months × 0.6
ELSE IF supply_delay_months >= 3 THEN
  delay_severity = "medium"
  estimated_delay_months = supply_delay_months × 0.4
ELSE
  delay_severity = "low"
  estimated_delay_months = supply_delay_months × 0.2

-- 代替調達先がある場合の補正
IF secondary_source_available THEN
  estimated_delay_months = estimated_delay_months × 0.5
```

### Step 3: ペナルティ金額の算出

```
-- 納期遅延ペナルティのリスク金額
at_risk_penalty_projects = MIN(projects_with_penalty_clause, CEIL(at_risk_projects))

monthly_penalty_per_project = avg_project_contract_amount_jpy × (avg_penalty_rate_pct / 100)
total_penalty_risk_jpy = at_risk_penalty_projects × monthly_penalty_per_project × estimated_delay_months

-- ペナルティ上限（通常は契約金額の10-20%）
penalty_cap_per_project = avg_project_contract_amount_jpy × 0.15
total_penalty_capped_jpy = MIN(total_penalty_risk_jpy, at_risk_penalty_projects × penalty_cap_per_project)
```

### Step 4: エンジニアリソース遊休コストの算出

```
-- 調達遅延によるエンジニア待機コスト
IF delay_severity IN ("critical", "high") THEN
  idle_engineer_ratio = 0.6  -- 調達待ちで作業不能な比率
ELSE IF delay_severity == "medium" THEN
  idle_engineer_ratio = 0.3
ELSE
  idle_engineer_ratio = 0.1

idle_engineers = CEIL(total_assigned_engineers × idle_engineer_ratio × affected_vendor_ratio)
monthly_idle_cost_jpy = idle_engineers × avg_engineer_hourly_rate_jpy × 160  -- 月160時間
total_idle_cost_jpy = monthly_idle_cost_jpy × estimated_delay_months

-- エンジニア再配置可能性
IF idle_engineers <= active_projects_count × 0.1 THEN
  reallocation_feasibility = "easy"
  effective_idle_cost = total_idle_cost_jpy × 0.3
ELSE IF idle_engineers <= active_projects_count × 0.3 THEN
  reallocation_feasibility = "moderate"
  effective_idle_cost = total_idle_cost_jpy × 0.6
ELSE
  reallocation_feasibility = "difficult"
  effective_idle_cost = total_idle_cost_jpy × 0.9
```

### Step 5: 調達コスト増加の評価

```
-- HW調達コスト増加の影響
additional_procurement_cost = total_hw_procurement_budget_jpy × (price_increase_pct / 100) × affected_vendor_ratio

-- プロジェクト採算への影響
IF price_increase_pct >= 25 THEN
  profitability_impact = "severe"
  margin_erosion_pct = price_increase_pct × 0.4  -- HW比率を考慮
ELSE IF price_increase_pct >= 15 THEN
  profitability_impact = "significant"
  margin_erosion_pct = price_increase_pct × 0.3
ELSE IF price_increase_pct >= 5 THEN
  profitability_impact = "moderate"
  margin_erosion_pct = price_increase_pct × 0.2
ELSE
  profitability_impact = "minimal"
  margin_erosion_pct = price_increase_pct × 0.1

-- スポット調達プレミアム
IF delay_severity IN ("critical", "high") AND NOT secondary_source_available THEN
  spot_premium_pct = price_increase_pct × 1.5
  spot_procurement_cost = total_hw_procurement_budget_jpy × affected_vendor_ratio × (spot_premium_pct / 100)
ELSE
  spot_premium_pct = 0.0
  spot_procurement_cost = 0
```

### Step 6: 新規受注・パイプラインへの影響

```
-- HW調達を含む新規案件の受注判断への影響
IF delay_severity == "critical" THEN
  new_deal_impact = "受注見送り推奨"
  pipeline_at_risk_ratio = 0.4
ELSE IF delay_severity == "high" THEN
  new_deal_impact = "条件付き受注（納期バッファ必須）"
  pipeline_at_risk_ratio = 0.25
ELSE IF delay_severity == "medium" THEN
  new_deal_impact = "リスク注記付きで受注可"
  pipeline_at_risk_ratio = 0.10
ELSE
  new_deal_impact = "通常受注"
  pipeline_at_risk_ratio = 0.0

pipeline_at_risk_jpy = backlog_pipeline_jpy × pipeline_at_risk_ratio
```

### Step 7: 総合インパクトスコア算出

```
delay_severity_weight:
  critical → 1.0
  high     → 0.7
  medium   → 0.4
  low      → 0.1

profitability_weight:
  severe      → 1.0
  significant → 0.7
  moderate    → 0.4
  minimal     → 0.1

project_risk_factor = MIN(1.0, project_risk_ratio × 3)

-- 総財務影響
total_financial_impact = total_penalty_capped_jpy + effective_idle_cost + additional_procurement_cost
financial_materiality = MIN(1.0, total_financial_impact / (avg_project_contract_amount_jpy × active_projects_count × 0.1))

raw_score = (delay_severity_weight × 0.25
           + project_risk_factor × 0.25
           + financial_materiality × 0.20
           + profitability_weight × 0.15
           + MIN(1.0, pipeline_at_risk_ratio × 4) × 0.15)
           × 100

impact_score = CLAMP(raw_score, 0, 100)
```

### Step 8: インパクトレベル判定

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
| `at_risk_projects` | int | 影響を受けるプロジェクト数 |
| `estimated_delay_months` | float | 推定遅延期間（月） |
| `total_penalty_risk_jpy` | int | ペナルティリスク金額（円） |
| `idle_engineers` | int | 遊休化リスクのあるエンジニア数 |
| `effective_idle_cost_jpy` | int | 実効エンジニア遊休コスト（円） |
| `additional_procurement_cost_jpy` | int | 追加調達コスト（円） |
| `profitability_impact` | enum | プロジェクト採算影響レベル |
| `new_deal_impact` | string | 新規受注判断への影響 |
| `pipeline_at_risk_jpy` | int | リスクのあるパイプライン金額（円） |
| `recommended_actions` | list[string] | 推奨アクションリスト |
| `notification_channel` | string | 推奨通知チャネル |
| `risk_summary` | string | リスクサマリー文（自然言語） |

### 推奨アクション分岐

| レベル | 推奨アクション | 通知チャネル |
|---|---|---|
| critical | 影響プロジェクトの顧客への即時通知と納期再交渉の開始、ペナルティ条項の免責（不可抗力）適用可否の法務確認、代替ベンダー・中古/リファービッシュ機器の緊急調達手配、遊休エンジニアの他プロジェクトへの即時再配置、HW調達を含む新規案件の受注一時凍結、プロジェクトポートフォリオ全体の再スケジュール、経営会議への報告・引当金計上の検討 | slack + email |
| high | 影響プロジェクトの洗い出しと優先順位付け、顧客への早期情報共有と納期バッファの交渉、代替調達先の選定・見積取得、クリティカルパス上のHW調達タスクの前倒し検討、エンジニア配置計画の見直し、新規案件の提案時に納期バッファ（+3ヶ月）の組み込み | slack |
| medium | ベンダーへの納期確認・情報収集、影響を受けるプロジェクトのリスク台帳への登録、調達リードタイムを考慮したプロジェクト計画の見直し、エンジニアのスキルアップ・教育への時間活用検討 | slack |
| low | 半導体市場動向のモニタリング継続、長期調達契約（フレーム契約）の見直し検討、次年度の調達戦略への反映 | 定期レポート |

### 通知テンプレート（critical / high 時）

```
件名: 【{impact_level}】半導体供給制約によるSIプロジェクト納期遅延リスク
本文:
半導体供給制約に伴い、SI事業のプロジェクト進行に
重大な影響が見込まれます。
- インパクトスコア: {impact_score}/100
- 影響プロジェクト数: {at_risk_projects}件 / {active_projects_count}件
- 推定遅延: {estimated_delay_months:.1f}ヶ月
- ペナルティリスク: ¥{total_penalty_risk_jpy:,}
- エンジニア遊休: {idle_engineers}名（コスト: ¥{effective_idle_cost_jpy:,}）
- 追加調達コスト: ¥{additional_procurement_cost_jpy:,}
- 新規受注判断: {new_deal_impact}
- パイプラインリスク: ¥{pipeline_at_risk_jpy:,}

影響プロジェクトの顧客への早期連絡と
代替調達手段の確保を進めてください。
```

## 参照データソース（Fabric テーブル）

| テーブル名 | DB | 説明 | 主要カラム |
|---|---|---|---|
| `si_customers` | sqldb_si_01 | SI事業の顧客（法人）マスタ | `si_customer_id`, `company_name`, `industry_code`, `status` |
| `si_projects` | sqldb_si_01 | プロジェクト管理 | `project_id`, `si_customer_id`, `project_name`, `project_type`, `contract_amount`, `start_date`, `end_date`, `status`, `assigned_engineer_count` |
| `si_contracts` | sqldb_si_01 | 契約情報 | `contract_id`, `si_customer_id`, `project_id`, `contract_type`, `total_amount`, `start_date`, `end_date`, `status` |
| `si_transactions` | sqldb_si_01 | 取引履歴 | `transaction_id`, `si_customer_id`, `contract_id`, `transaction_type`, `amount`, `transaction_date` |
| `si_engineers` | sqldb_si_01 | エンジニアリソース管理 | `engineer_id`, `employee_name`, `skill_set`, `grade`, `hourly_rate`, `availability_status`, `current_project_id` |
| `unified_customers` | sqldb_common_01 | 統合顧客マスタ | `unified_customer_id`, `full_name`, `email`, `phone_number` |
| `domain_id_mappings` | sqldb_common_01 | 業務領域IDマッピング | `unified_customer_id`, `domain_code`, `domain_customer_id`, `is_active` |
| `customer_segments` | sqldb_common_01 | 顧客セグメント | `unified_customer_id`, `segment_code`, `risk_score`, `lifetime_value` |

## データ品質メモ

- `si_projects` の `status = 'IN_PROGRESS'` でフィルタして進行中プロジェクト数を取得する。`project_type` にインフラ構築（`INFRASTRUCTURE`, `MIGRATION`等）を含むプロジェクトがHW調達対象の候補。
- `si_projects.assigned_engineer_count` でプロジェクト別のアサイン人数を取得できるが、`si_engineers.current_project_id` との突合で正確な配置状況を確認すること。
- `si_contracts.contract_type` がペナルティ条項の有無を直接示すカラムではないため、ペナルティ情報は契約書管理システムまたは `description` カラムのパターン分析から補完する前提。
- `si_engineers.availability_status = 'IDLE'` のエンジニアは既に遊休状態であり、調達遅延による追加遊休とは区別する。`availability_status = 'ASSIGNED'` かつ `current_project_id` が影響プロジェクトに該当するエンジニアが遊休化リスク対象。
- `si_transactions.transaction_type = 'HW_PROCUREMENT'` の合計がHW調達実績額の近似値となる。予算ベースの分析には契約データ（`si_contracts`）を使用する。
- `customer_segments.segment_code = 'PREMIUM'` の法人顧客はプロジェクト規模が大きい傾向があり、遅延時のペナルティ・レピュテーション影響が顕著。優先的に顧客コミュニケーションを行うことを推奨。
