# SI事業：クラウドサイバー攻撃インパクト判断スキル

## 前提

- 本文書は nexus6-se-workshop の Demo 用ナレッジであり、数値・事例はすべて想定値である。
- 個人情報、実在企業の機密、実顧客の行動履歴は含めない。
- 対象事業部は SI事業 である。
- 対応シナリオは「国内主要クラウドリージョンへの大規模サイバー攻撃（サイバーセキュリティリスク）」。
- Agent 2 は影響有無の評価、Agent 3 は事業部別 Next Action の草案作成に使う。

## 概要

国内大手クラウドプロバイダーの東京リージョンがDDoS攻撃とランサムウェアの複合攻撃を受けた際に、SI事業が顧客向けに構築・運用するシステムの稼働停止リスク、SLA違反に伴うペナルティ・損害賠償、顧客対応に必要なエンジニアリソースの逼迫、顧客データの漏洩・毀損リスク、および顧客信頼の毀損を総合的に判断するスキル。影響を受ける顧客システム数、SLA違反金額、エンジニア緊急対応コスト、データ安全性を定量評価し、推奨アクションを出力する。

## 適用シナリオ

- 外部ニュースのトリガー: クラウドプロバイダーの障害報告、大規模DDoS攻撃の報道、ランサムウェア被害報告、東京リージョンの複数AZ障害。
- SI事業は顧客のシステムをクラウド上に構築・運用する立場であり、クラウド障害時に「顧客から見た責任者」として最前線に立つ。クラウドプロバイダーの障害であっても、顧客への説明責任・復旧対応義務はSI事業が負う。
- 複数顧客のシステムが同時に影響を受けるため、エンジニアリソースの配分が最大の課題となる。

## 入力パラメータ

### ニュース情報

| パラメータ | 型 | 説明 |
|---|---|---|
| `news_category` | string | ニュースカテゴリ（`cyber_security`） |
| `attack_type` | list[string] | 攻撃種別（例: `DDoS`, `ransomware`, `supply_chain`, `zero_day`） |
| `affected_cloud_provider` | string | 影響クラウドプロバイダー名 |
| `affected_region` | string | 影響リージョン（例: `ap-northeast-1`） |
| `affected_az_count` | int | 影響AZ数 |
| `total_az_count` | int | 当該リージョン総AZ数 |
| `estimated_downtime_hours` | float | 推定障害時間（時間） |
| `data_breach_confirmed` | bool | データ漏洩の確認有無 |
| `attack_attribution` | enum | `nation_state` / `criminal_group` / `unknown` |

### 業務データ

| パラメータ | 型 | 説明 | 参照テーブル |
|---|---|---|---|
| `total_managed_systems` | int | 運用管理中の顧客システム総数 | `si_projects` (status=MAINTENANCE) |
| `systems_on_affected_region` | int | 影響リージョン上の顧客システム数 | `si_projects` + インフラ台帳 |
| `systems_currently_down` | int | 現在停止中の顧客システム数 | 監視システム |
| `systems_with_sla` | int | SLA契約付きの運用システム数 | `si_contracts` 分析 |
| `avg_sla_penalty_per_hour_jpy` | int | SLA違反時の平均ペナルティ（円/時間/システム） | `si_contracts` 分析 |
| `systems_with_dr` | int | DR環境を持つシステム数 | インフラ台帳 |
| `avg_dr_failover_minutes` | int | DR切替の平均所要時間（分） | BCP計画書 |
| `available_engineers` | int | 即時対応可能なエンジニア数 | `si_engineers` (availability=AVAILABLE) |
| `total_on_call_engineers` | int | オンコール体制のエンジニア数 | `si_engineers` 分析 |
| `engineer_per_system_required` | float | 復旧対応に必要なエンジニア数/システム | 運用基準書 |
| `affected_customers_count` | int | 影響を受ける顧客数 | `si_customers` + `si_projects` |
| `premium_customers_affected` | int | 影響を受けるPREMIUM顧客数 | `customer_segments` 分析 |
| `customer_data_on_affected_region` | bool | 顧客データが影響リージョンに格納されているか | インフラ台帳 |
| `backup_region` | string | バックアップデータの保管リージョン | バックアップ管理 |
| `last_backup_hours_ago` | float | 最終バックアップからの経過時間 | バックアップ管理 |

## 判断ロジック

### Step 1: 顧客システム影響範囲の判定

```
system_down_ratio = systems_currently_down / MAX(1, systems_on_affected_region)
az_impact_ratio = affected_az_count / total_az_count

IF system_down_ratio >= 0.8 THEN
  system_impact = "total"
ELSE IF system_down_ratio >= 0.5 THEN
  system_impact = "major"
ELSE IF system_down_ratio > 0 THEN
  system_impact = "partial"
ELSE
  system_impact = "none"

-- 影響を受けるシステムの全体比率
overall_impact_ratio = systems_on_affected_region / MAX(1, total_managed_systems)
```

### Step 2: SLA違反・ペナルティ金額の算出

```
-- DR切替による実効停止時間
IF systems_with_dr > 0 THEN
  dr_coverage = systems_with_dr / MAX(1, systems_on_affected_region)
  effective_downtime_dr_systems = MIN(estimated_downtime_hours, avg_dr_failover_minutes / 60 + 0.5)
  effective_downtime_non_dr = estimated_downtime_hours
  weighted_downtime = effective_downtime_dr_systems × dr_coverage + effective_downtime_non_dr × (1.0 - dr_coverage)
ELSE
  weighted_downtime = estimated_downtime_hours
  dr_coverage = 0.0

-- SLA違反によるペナルティ
sla_affected_systems = MIN(systems_with_sla, systems_currently_down)
total_sla_penalty_jpy = sla_affected_systems × avg_sla_penalty_per_hour_jpy × weighted_downtime

-- SLAのペナルティ上限（通常は月額費用の30%）
-- ここでは概算として算出し、上限は個別契約で確認
IF weighted_downtime >= 24 THEN
  sla_severity = "critical"
ELSE IF weighted_downtime >= 8 THEN
  sla_severity = "high"
ELSE IF weighted_downtime >= 2 THEN
  sla_severity = "medium"
ELSE
  sla_severity = "low"
```

### Step 3: エンジニアリソース逼迫度の評価

```
-- 復旧対応に必要なエンジニア数
required_engineers = CEIL(systems_currently_down × engineer_per_system_required)

-- 即時対応可能なリソース
immediate_capacity = available_engineers + total_on_call_engineers

IF required_engineers > immediate_capacity × 1.5 THEN
  resource_strain = "critical"
  engineer_shortage = required_engineers - immediate_capacity
  -- 外部パートナー/ベンダーへの応援要請が必要
  external_support_needed = TRUE
ELSE IF required_engineers > immediate_capacity THEN
  resource_strain = "high"
  engineer_shortage = required_engineers - immediate_capacity
  external_support_needed = TRUE
ELSE IF required_engineers > immediate_capacity × 0.7 THEN
  resource_strain = "medium"
  engineer_shortage = 0
  external_support_needed = FALSE
ELSE
  resource_strain = "low"
  engineer_shortage = 0
  external_support_needed = FALSE

-- 対応優先順位の必要性
IF systems_currently_down > immediate_capacity / engineer_per_system_required THEN
  triage_needed = TRUE
  -- PREMIUMセグメント顧客のシステムを優先
ELSE
  triage_needed = FALSE
```

### Step 4: 顧客データ安全性リスクの評価

```
IF data_breach_confirmed AND customer_data_on_affected_region THEN
  data_risk = "critical"
  -- 顧客データ漏洩 → 全顧客への通知義務・損害賠償リスク
  breach_notification_required = TRUE
  estimated_breach_cost_per_customer_jpy = 5000000  -- 500万円/社（法人顧客）
  total_breach_liability_jpy = affected_customers_count × estimated_breach_cost_per_customer_jpy
ELSE IF "ransomware" IN attack_type AND backup_region == affected_region THEN
  data_risk = "high"
  breach_notification_required = FALSE
  total_breach_liability_jpy = 0
ELSE IF "ransomware" IN attack_type AND last_backup_hours_ago > 24 THEN
  data_risk = "high"
  breach_notification_required = FALSE
  total_breach_liability_jpy = 0
ELSE IF backup_region != affected_region AND last_backup_hours_ago <= 6 THEN
  data_risk = "low"
  breach_notification_required = FALSE
  total_breach_liability_jpy = 0
ELSE
  data_risk = "medium"
  breach_notification_required = FALSE
  total_breach_liability_jpy = 0
```

### Step 5: 顧客コミュニケーション負荷の評価

```
-- 顧客への状況報告の負荷
IF affected_customers_count >= 20 THEN
  communication_load = "extreme"
  -- 個別連絡は困難。一斉通知＋ステータスページの運用が必要
  individual_contact_feasible = FALSE
ELSE IF affected_customers_count >= 10 THEN
  communication_load = "high"
  individual_contact_feasible = TRUE  -- ただし全員への即時対応は困難
ELSE IF affected_customers_count >= 5 THEN
  communication_load = "medium"
  individual_contact_feasible = TRUE
ELSE
  communication_load = "low"
  individual_contact_feasible = TRUE

-- PREMIUM顧客への優先対応
premium_ratio = premium_customers_affected / MAX(1, affected_customers_count)
IF premium_ratio >= 0.5 THEN
  premium_urgency = "high"
ELSE IF premium_ratio >= 0.2 THEN
  premium_urgency = "medium"
ELSE
  premium_urgency = "low"
```

### Step 6: 攻撃の持続性・二次被害リスクの評価

```
-- 攻撃の持続性判定
IF attack_attribution == "nation_state" AND "ransomware" IN attack_type THEN
  persistence_risk = "critical"
ELSE IF attack_attribution == "nation_state" THEN
  persistence_risk = "high"
ELSE IF LEN(attack_type) >= 2 THEN  -- 複合攻撃
  persistence_risk = "high"
ELSE
  persistence_risk = "medium"

-- 二次被害リスク（攻撃がSI事業の管理ネットワークに波及するリスク）
IF "supply_chain" IN attack_type OR "zero_day" IN attack_type THEN
  lateral_movement_risk = "high"
  -- 自社管理環境のセキュリティ点検が必要
ELSE
  lateral_movement_risk = "low"
```

### Step 7: 総合インパクトスコア算出

```
system_impact_weight:
  total   → 1.0
  major   → 0.7
  partial → 0.4
  none    → 0.0

data_risk_weight:
  critical → 1.0
  high     → 0.7
  medium   → 0.4
  low      → 0.1

resource_strain_weight:
  critical → 1.0
  high     → 0.7
  medium   → 0.4
  low      → 0.1

sla_severity_weight:
  critical → 1.0
  high     → 0.7
  medium   → 0.4
  low      → 0.1

communication_load_weight:
  extreme → 1.0
  high    → 0.7
  medium  → 0.4
  low     → 0.1

raw_score = (system_impact_weight × 0.25
           + data_risk_weight × 0.20
           + resource_strain_weight × 0.20
           + sla_severity_weight × 0.20
           + communication_load_weight × 0.15)
           × 100

-- DR カバレッジによる緩和
dr_mitigation = dr_coverage × 0.15 × 100
raw_score = raw_score - dr_mitigation

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
| `system_impact` | enum | 顧客システム影響レベル（`total`/`major`/`partial`/`none`） |
| `systems_currently_down` | int | 停止中の顧客システム数 |
| `effective_downtime_hours` | float | 実効停止時間（DR考慮後加重平均） |
| `total_sla_penalty_jpy` | int | SLAペナルティ推定額（円） |
| `engineer_shortage` | int | エンジニア不足数 |
| `resource_strain` | enum | リソース逼迫度 |
| `data_risk_level` | enum | データ安全性リスクレベル |
| `total_breach_liability_jpy` | int | データ漏洩時の損害賠償推定額（円） |
| `affected_customers_count` | int | 影響顧客数 |
| `triage_needed` | bool | 対応トリアージの要否 |
| `recommended_actions` | list[string] | 推奨アクションリスト |
| `notification_channel` | string | 推奨通知チャネル |
| `risk_summary` | string | リスクサマリー文（自然言語） |

### 推奨アクション分岐

| レベル | 推奨アクション | 通知チャネル |
|---|---|---|
| critical | 顧客システムの対応トリアージ実施（PREMIUM顧客優先）、DR環境への即時フェイルオーバー指示、全影響顧客への第一報（障害認知・復旧見通し）の一斉送信、エンジニアの全員召集と対応チーム編成、外部パートナー・ベンダーへの応援要請、CSIRT起動とデータ漏洩フォレンジック調査の開始、自社管理ネットワークへの二次被害防止措置（セグメント隔離・特権アカウント凍結）、経営層・顧客責任者への30分間隔のステータス更新 | slack + email + phone |
| high | DR切替準備の即時開始、影響顧客への個別連絡（担当PM経由）、エンジニアのオンコール発動と追加動員検討、顧客ステータスページの公開、バックアップデータの安全性確認、セキュリティログの緊急監査開始 | slack + email |
| medium | 影響を受ける可能性のあるシステムの稼働監視強化（1分間隔）、エンジニア待機体制の確保、クラウドプロバイダーへの状況確認と復旧見通し取得、顧客PMへの事前連絡（状況説明＋エスカレーション経路共有） | slack |
| low | クラウドプロバイダーの復旧状況モニタリング、マルチクラウド・マルチリージョン構成の提案機会の検討、BCP/DR強化の顧客提案資料の準備 | 定期レポート |

### 通知テンプレート（critical / high 時）

```
件名: 【{impact_level}】クラウド攻撃による顧客システム稼働停止
本文:
クラウドリージョンへのサイバー攻撃に伴い、
SI事業が運用管理する顧客システムに影響が発生しています。

■ 影響状況
- インパクトスコア: {impact_score}/100
- 停止中システム: {systems_currently_down}件 / {systems_on_affected_region}件
- 影響顧客数: {affected_customers_count}社
  （うちPREMIUM: {premium_customers_affected}社）
- 実効停止時間: {effective_downtime_hours:.1f}時間

■ 財務影響
- SLAペナルティ推定: ¥{total_sla_penalty_jpy:,}
- データ漏洩リスク: {data_risk_level}

■ リソース状況
- エンジニア逼迫度: {resource_strain}
- 不足エンジニア数: {engineer_shortage}名
- トリアージ要否: {triage_needed}

PREMIUM顧客のシステム復旧を最優先とし、
全顧客への第一報を速やかに発信してください。
```

## 参照データソース（Fabric テーブル）

| テーブル名 | DB | 説明 | 主要カラム |
|---|---|---|---|
| `si_customers` | sqldb_si_01 | SI事業の顧客（法人）マスタ | `si_customer_id`, `company_name`, `industry_code`, `contact_email`, `status` |
| `si_projects` | sqldb_si_01 | プロジェクト管理 | `project_id`, `si_customer_id`, `project_name`, `project_type`, `contract_amount`, `start_date`, `end_date`, `status`, `assigned_engineer_count` |
| `si_contracts` | sqldb_si_01 | 契約情報 | `contract_id`, `si_customer_id`, `project_id`, `contract_type`, `total_amount`, `start_date`, `end_date`, `status` |
| `si_transactions` | sqldb_si_01 | 取引履歴 | `transaction_id`, `si_customer_id`, `contract_id`, `transaction_type`, `amount`, `transaction_date` |
| `si_engineers` | sqldb_si_01 | エンジニアリソース管理 | `engineer_id`, `employee_name`, `skill_set`, `grade`, `hourly_rate`, `availability_status`, `current_project_id` |
| `unified_customers` | sqldb_common_01 | 統合顧客マスタ | `unified_customer_id`, `full_name`, `email`, `phone_number` |
| `domain_id_mappings` | sqldb_common_01 | 業務領域IDマッピング | `unified_customer_id`, `domain_code`, `domain_customer_id`, `is_active` |
| `customer_segments` | sqldb_common_01 | 顧客セグメント | `unified_customer_id`, `segment_code`, `risk_score`, `lifetime_value` |

## データ品質メモ

- `si_projects` の `status = 'MAINTENANCE'` でフィルタして運用中システム数を取得する。`project_type` にクラウド関連（`CLOUD_MIGRATION`, `INFRASTRUCTURE`, `MANAGED_SERVICE`等）を含むプロジェクトが影響対象の候補。
- `si_engineers.availability_status` が `AVAILABLE` のエンジニアが即時対応可能。`ON_CALL` のエンジニアは召集後30分〜1時間で対応開始可能と想定。`ASSIGNED` のエンジニアは現在のプロジェクトから引き剥がす判断が必要。
- `si_engineers.skill_set` に `cloud`, `aws`, `azure`, `security`, `incident_response` 等のキーワードを含むエンジニアを優先的にアサインすること。JSON配列形式で格納。
- `si_contracts.contract_type` に `SLA`, `MANAGED_SERVICE` 等を含む契約がSLAペナルティの対象。ペナルティ金額の詳細は個別契約書を確認する必要があるため、`avg_sla_penalty_per_hour_jpy` は概算値。
- `customer_segments` で `segment_code = 'PREMIUM'` の顧客を特定し、復旧対応の優先順位付けに使用する。`domain_id_mappings` の `domain_code = 'SI'` で結合して顧客を特定すること。
- SI事業特有の留意点: 複数顧客のシステムが同一リージョン上で稼働している場合、1社の復旧作業が他社に波及するリスク（リソース競合）がある。エンジニアの並行対応可能数を考慮したトリアージが不可欠。
- インシデント発生時のエンジニア対応工数（残業・休日出勤）は `si_transactions` に `INCIDENT_RESPONSE` として計上されるが、リアルタイムでは反映されないため、別途の工数管理ツールで追跡すること。
