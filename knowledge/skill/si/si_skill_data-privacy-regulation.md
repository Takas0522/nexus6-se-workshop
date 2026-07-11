# SI事業：改正個人情報保護法インパクト判断スキル

## 前提

- 本文書は nexus6-se-workshop の Demo 用ナレッジであり、数値・事例はすべて想定値である。
- 個人情報、実在企業の機密、実顧客の行動履歴は含めない。
- 対象事業部は SI事業 である。
- 対応シナリオは「改正個人情報保護法によるデータ規制強化（法規制・コンプライアンスリスク）」。
- Agent 2 は影響有無の評価、Agent 3 は事業部別 Next Action の草案作成に使う。

## 概要

2026年施行の改正個人情報保護法により、SI事業が顧客向けに構築・運用するシステムにおける個人データ処理への影響を判断するスキル。SI事業は自社の広告収益モデルを持たないが、顧客企業のシステムを受託開発・運用する立場として、(1) 既存システムの法令対応改修の受注機会、(2) 受託運用中のシステムの法令違反リスク（連帯責任）、(3) 個人データを扱うプロジェクトの契約条件見直し、の3軸でインパクトを評価する。

## 適用シナリオ

- 外部ニュースのトリガー: 改正法の施行発表、ガイドライン公布、同業他社のSIベンダーへの制裁金事例報道、委託先の安全管理義務強化の報道。
- SI事業は「データ処理の委託先」として顧客企業の個人データを取り扱うケースが多く、委託元・委託先双方の義務が強化される点が固有のリスク。
- 一方で、法改正に伴うシステム改修需要は新規受注の商機でもある。リスクと機会の両面を評価する。

## 入力パラメータ

### ニュース情報

| パラメータ | 型 | 説明 |
|---|---|---|
| `news_category` | string | ニュースカテゴリ（`data_privacy_regulation`） |
| `regulation_name` | string | 法令名（例: `改正個人情報保護法`） |
| `enforcement_date` | date | 施行日 |
| `consent_requirement` | string | 同意要件（例: `explicit_optin`, `opt_out`） |
| `third_party_sharing_restricted` | bool | 第三者提供制限の有無 |
| `processor_obligations_enhanced` | bool | 委託先（処理者）の義務強化の有無 |
| `max_penalty_pct_of_revenue` | float | 最大制裁金率（売上高比%） |
| `transition_period_months` | int | 経過措置期間（月） |
| `data_breach_notification_hours` | int | 漏洩通知義務の期限（時間） |

### 業務データ

| パラメータ | 型 | 説明 | 参照テーブル |
|---|---|---|---|
| `active_projects_count` | int | 進行中プロジェクト数 | `si_projects` (status=IN_PROGRESS) |
| `projects_handling_personal_data` | int | 個人データを取り扱うプロジェクト数 | `si_projects` 分析 |
| `projects_in_operations_phase` | int | 運用保守フェーズのプロジェクト数 | `si_projects` (status=MAINTENANCE) |
| `ops_projects_with_personal_data` | int | 運用保守中で個人データを扱うプロジェクト数 | `si_projects` 分析 |
| `total_contract_amount_jpy` | int | 進行中プロジェクトの契約金額合計（円） | `si_contracts` 集計 |
| `annual_revenue_jpy` | int | SI事業の年間売上高（円） | 財務データ |
| `dpa_signed_ratio` | float | データ処理契約（DPA）締結済み比率（0.0〜1.0） | 法務管理 |
| `privacy_certified_engineers` | int | プライバシー関連資格保有エンジニア数 | `si_engineers` 分析 |
| `total_engineers` | int | エンジニア総数 | `si_engineers` 集計 |
| `security_audit_completed_ratio` | float | セキュリティ監査完了比率（個人データPJ対象、0.0〜1.0） | 監査管理 |
| `client_industries_affected` | int | 法改正の影響を強く受ける業種の顧客数（小売・金融・医療等） | `si_customers` 分析 |
| `total_clients` | int | アクティブ顧客数 | `si_customers` (status=ACTIVE) |
| `existing_compliance_projects` | int | 既存の法令対応関連プロジェクト数 | `si_projects` 分析 |

## 判断ロジック

### Step 1: 受託システムの法令違反リスク評価

```
-- 運用中システムの違反リスク
IF processor_obligations_enhanced AND ops_projects_with_personal_data > 0 THEN
  non_compliant_ops_estimate = ops_projects_with_personal_data × (1.0 - dpa_signed_ratio)
ELSE
  non_compliant_ops_estimate = 0

IF non_compliant_ops_estimate >= 10 THEN
  ops_compliance_risk = "critical"
ELSE IF non_compliant_ops_estimate >= 5 THEN
  ops_compliance_risk = "high"
ELSE IF non_compliant_ops_estimate > 0 THEN
  ops_compliance_risk = "medium"
ELSE
  ops_compliance_risk = "low"

-- 開発中システムの対応漏れリスク
non_compliant_dev_estimate = projects_handling_personal_data × (1.0 - security_audit_completed_ratio)
```

### Step 2: 制裁金リスクの算出

```
max_penalty_jpy = annual_revenue_jpy × (max_penalty_pct_of_revenue / 100)

-- SI事業は委託先としての連帯責任リスク
-- 直接の制裁金に加え、顧客からの損害賠償リスクも考慮
IF ops_compliance_risk == "critical" THEN
  violation_probability = "high"
  expected_penalty_jpy = max_penalty_jpy × 0.20
  -- 顧客からの損害賠償リスク（制裁金の追加分）
  customer_claim_risk_jpy = expected_penalty_jpy × 0.5
ELSE IF ops_compliance_risk == "high" THEN
  violation_probability = "medium"
  expected_penalty_jpy = max_penalty_jpy × 0.10
  customer_claim_risk_jpy = expected_penalty_jpy × 0.3
ELSE IF ops_compliance_risk == "medium" THEN
  violation_probability = "low"
  expected_penalty_jpy = max_penalty_jpy × 0.03
  customer_claim_risk_jpy = expected_penalty_jpy × 0.1
ELSE
  violation_probability = "minimal"
  expected_penalty_jpy = 0
  customer_claim_risk_jpy = 0

total_financial_risk_jpy = expected_penalty_jpy + customer_claim_risk_jpy
```

### Step 3: 受注機会の評価（ポジティブインパクト）

```
-- 法改正に伴うシステム改修需要
affected_client_ratio = client_industries_affected / MAX(1, total_clients)

IF affected_client_ratio >= 0.5 AND transition_period_months <= 12 THEN
  opportunity_level = "large"
  estimated_new_deals = client_industries_affected × 0.3
  avg_compliance_project_value_jpy = 15000000  -- 1500万円/案件
ELSE IF affected_client_ratio >= 0.3 THEN
  opportunity_level = "medium"
  estimated_new_deals = client_industries_affected × 0.2
  avg_compliance_project_value_jpy = 10000000
ELSE
  opportunity_level = "small"
  estimated_new_deals = client_industries_affected × 0.1
  avg_compliance_project_value_jpy = 8000000

estimated_opportunity_jpy = estimated_new_deals × avg_compliance_project_value_jpy

-- 既存の法令対応プロジェクトがある場合、追加受注が期待できる
IF existing_compliance_projects > 0 THEN
  upsell_opportunity_jpy = existing_compliance_projects × avg_compliance_project_value_jpy × 0.5
ELSE
  upsell_opportunity_jpy = 0
```

### Step 4: エンジニア体制の準備度評価

```
privacy_engineer_ratio = privacy_certified_engineers / MAX(1, total_engineers)

IF privacy_engineer_ratio >= 0.15 THEN
  engineer_readiness = "sufficient"
ELSE IF privacy_engineer_ratio >= 0.08 THEN
  engineer_readiness = "partial"
ELSE IF privacy_engineer_ratio >= 0.03 THEN
  engineer_readiness = "insufficient"
ELSE
  engineer_readiness = "critical_gap"

-- 必要なエンジニア数の推定
required_privacy_engineers = projects_handling_personal_data × 0.5 + ops_projects_with_personal_data × 0.3
engineer_gap = MAX(0, required_privacy_engineers - privacy_certified_engineers)

-- 育成コスト
training_cost_per_engineer = 500000  -- 50万円/人（研修・資格取得）
total_training_cost_jpy = engineer_gap × training_cost_per_engineer
```

### Step 5: 契約条件への影響評価

```
-- 既存契約の見直し必要性
IF processor_obligations_enhanced THEN
  contracts_needing_revision = projects_handling_personal_data × (1.0 - dpa_signed_ratio) + ops_projects_with_personal_data × (1.0 - dpa_signed_ratio)
ELSE
  contracts_needing_revision = 0

-- 施行までの残り時間
days_to_enforcement = (enforcement_date - TODAY).days

IF days_to_enforcement <= 90 AND contracts_needing_revision > 5 THEN
  contract_urgency = "critical"
ELSE IF days_to_enforcement <= 180 AND contracts_needing_revision > 3 THEN
  contract_urgency = "high"
ELSE IF contracts_needing_revision > 0 THEN
  contract_urgency = "medium"
ELSE
  contract_urgency = "low"

-- 漏洩通知義務の影響
IF data_breach_notification_hours <= 24 THEN
  incident_response_gap = "インシデント対応体制の即時整備が必要"
  ir_enhancement_needed = TRUE
ELSE IF data_breach_notification_hours <= 72 THEN
  incident_response_gap = "インシデント対応手順の見直しが必要"
  ir_enhancement_needed = TRUE
ELSE
  incident_response_gap = "現行体制で対応可能"
  ir_enhancement_needed = FALSE
```

### Step 6: 総合インパクトスコア算出

```
-- リスク側のスコア
ops_compliance_weight:
  critical → 1.0
  high     → 0.7
  medium   → 0.4
  low      → 0.1

contract_urgency_weight:
  critical → 1.0
  high     → 0.7
  medium   → 0.4
  low      → 0.1

engineer_readiness_penalty:
  critical_gap  → 0.3
  insufficient  → 0.2
  partial       → 0.1
  sufficient    → 0.0

financial_risk_factor = MIN(1.0, total_financial_risk_jpy / (annual_revenue_jpy × 0.02))

-- 機会側の補正（リスクを軽減する方向）
opportunity_offset = MIN(0.15, (estimated_opportunity_jpy + upsell_opportunity_jpy) / (annual_revenue_jpy × 0.05) × 0.15)

raw_score = (ops_compliance_weight × 0.25
           + contract_urgency_weight × 0.20
           + financial_risk_factor × 0.20
           + engineer_readiness_penalty × 0.15
           + MIN(1.0, non_compliant_dev_estimate / MAX(1, projects_handling_personal_data)) × 0.20)
           × 100
           - opportunity_offset × 100

impact_score = CLAMP(raw_score, 0, 100)
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
| `ops_compliance_risk` | enum | 運用中システムの法令違反リスク |
| `non_compliant_ops_estimate` | int | 未対応の運用プロジェクト推定数 |
| `total_financial_risk_jpy` | int | 総財務リスク（制裁金＋損害賠償） |
| `estimated_opportunity_jpy` | int | 法改正対応の受注機会金額（円） |
| `engineer_gap` | int | プライバシー人材の不足数 |
| `contracts_needing_revision` | int | 契約見直しが必要なプロジェクト数 |
| `contract_urgency` | enum | 契約見直しの緊急度 |
| `opportunity_level` | enum | 受注機会レベル |
| `ir_enhancement_needed` | bool | インシデント対応体制強化の要否 |
| `recommended_actions` | list[string] | 推奨アクションリスト |
| `notification_channel` | string | 推奨通知チャネル |
| `risk_summary` | string | リスクサマリー文（自然言語） |

### 推奨アクション分岐

| レベル | 推奨アクション | 通知チャネル |
|---|---|---|
| critical | 運用中の個人データ取扱いシステムの緊急棚卸し、全対象プロジェクトのDPA（データ処理契約）締結・改定の即時着手、顧客への法改正影響レポートの送付と改修提案の開始、インシデント対応体制の即時整備（通知期限対応）、プライバシーエンジニアの緊急採用・外部委託、開発標準・セキュリティガイドラインの改定、法務部門との連帯責任リスクの精査 | slack + email |
| high | 個人データ取扱いプロジェクトのリスクアセスメント実施、DPA未締結案件の優先対応リスト作成、影響顧客への法改正対応提案書の準備、プライバシー関連資格取得プログラムの実施、開発プロセスへのプライバシー・バイ・デザインの組み込み検討 | slack |
| medium | 個人データ取扱いプロジェクトの台帳整備、エンジニア向けプライバシー教育の計画策定、法改正対応ソリューションの営業資料作成、競合他社の対応動向調査 | slack |
| low | 法改正動向のモニタリング継続、次年度のプライバシー投資計画への反映、法改正対応実績の営業事例化 | 定期レポート |

### 通知テンプレート（critical / high 時）

```
件名: 【{impact_level}】改正個人情報保護法によるSI事業への影響（リスクと受注機会）
本文:
改正個人情報保護法の施行に伴い、SI事業に
リスクと受注機会の両面で影響が見込まれます。

■ リスク
- インパクトスコア: {impact_score}/100
- 運用中PJの法令違反リスク: {ops_compliance_risk}
  （未対応PJ: {non_compliant_ops_estimate}件）
- 財務リスク: ¥{total_financial_risk_jpy:,}
  （制裁金＋顧客損害賠償）
- DPA未締結/要改定PJ: {contracts_needing_revision}件
  （緊急度: {contract_urgency}）
- プライバシー人材不足: {engineer_gap}名

■ 受注機会
- 法改正対応の受注機会: ¥{estimated_opportunity_jpy:,}
- 機会レベル: {opportunity_level}

運用中システムのDPA整備を最優先とし、
並行して顧客への改修提案を進めてください。
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

- `si_projects.project_type` に `DATA_PLATFORM`, `CRM`, `ANALYTICS` 等のデータ関連カテゴリを含むプロジェクトが個人データ取扱い対象の候補。ただし明示的な「個人データフラグ」は存在しないため、プロジェクト名・種別からの推定が必要。
- `si_customers.industry_code` で小売（`RETAIL`）、金融（`FINANCE`）、医療（`HEALTHCARE`）等の業種を判定し、法改正の影響が大きい顧客を特定する。
- `si_engineers.skill_set` に `privacy`, `security`, `ISMS`, `CIPP` 等のキーワードを含むエンジニアを `privacy_certified_engineers` として集計する。JSON配列形式で格納されているため、JSONパース後にキーワード検索を行う。
- DPA締結状況（`dpa_signed_ratio`）はFabricテーブル外の法務管理システムから取得する前提。`si_contracts.contract_type` に `DPA` が含まれるケースもあるが網羅性は保証されない。
- `customer_segments.segment_code = 'PREMIUM'` の法人顧客は取引規模が大きく、法改正対応の提案先として優先度が高い。受注機会の試算にセグメント情報を活用すること。
- SI事業特有の留意点: 委託先としての連帯責任は、顧客企業の制裁金額（顧客の売上高の最大4%）に対する損害賠償請求となり得るため、自社売上高基準の制裁金計算とは別に顧客側の規模も考慮する必要がある。
