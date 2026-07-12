# 携帯電話事業：個人データ規制強化インパクト判断スキル

## 概要

EUデジタルサービス法（DSA）等の個人データ規制強化が携帯電話事業に与えるインパクトを判断するスキル。通信事業者が保有する顧客行動データ・位置情報の利活用制限、パートナー企業への提供制限、および違反時の制裁金リスクを評価し、データ収益事業への影響度と推奨アクションを出力する。

## 入力パラメータ

### ニュース情報

| パラメータ | 型 | 説明 |
|---|---|---|
| `news_category` | string | ニュースカテゴリ（`regulation_compliance`） |
| `regulation_name` | string | 規制名称（例: `EU_DSA`, `GDPR`, `電気通信事業法改正`） |
| `regulation_scope` | enum | `extraterritorial` / `domestic` / `sector_specific` |
| `penalty_type` | enum | `revenue_percentage` / `fixed_amount` / `operational_restriction` |
| `penalty_rate_percent` | float | 制裁金率（売上高に対する%）※revenue_percentage時 |
| `enforcement_timeline_months` | int | 施行までの猶予期間（月） |
| `data_categories_affected` | list[string] | 規制対象データ種別（例: `location`, `browsing_history`, `purchase_history`, `profiling`） |

### 業務データ

| パラメータ | 型 | 説明 |
|---|---|---|
| `data_monetization_revenue_annual_jpy` | int | データ利活用関連年間収益（円） |
| `total_annual_revenue_jpy` | int | 携帯電話事業年間売上高（円） |
| `eu_user_count` | int | EU域内ユーザー数 |
| `total_user_count` | int | 総ユーザー数 |
| `data_sharing_partner_count` | int | データ提供先パートナー企業数 |
| `consent_management_maturity` | enum | `advanced` / `standard` / `basic` / `none` |
| `privacy_compliance_staff_count` | int | プライバシー対応要員数 |
| `existing_certifications` | list[string] | 取得済み認証（例: `ISO27701`, `ISMS`, `TRUSTe`） |

## 判断ロジック

### Step 1: 規制の適用可能性判定

```
IF regulation_scope == "extraterritorial" AND eu_user_count > 0 THEN applicability = "direct"
ELSE IF regulation_scope == "domestic" THEN applicability = "direct"
ELSE IF regulation_scope == "sector_specific" THEN applicability = "conditional"
ELSE applicability = "indirect"

-- 間接適用の場合でも国内法への波及を考慮
IF applicability == "indirect" AND regulation_name IN ("EU_DSA", "GDPR") THEN
    applicability = "anticipated"  -- 国内規制への波及が予想される
```

### Step 2: 財務影響度の算出

```
-- 制裁金リスク額の算出
IF penalty_type == "revenue_percentage" THEN
    max_penalty_jpy = total_annual_revenue_jpy × (penalty_rate_percent / 100)
ELSE IF penalty_type == "fixed_amount" THEN
    max_penalty_jpy = penalty_fixed_amount_jpy
ELSE
    max_penalty_jpy = 0  -- 営業制限のみ

-- データ収益影響の推定（規制対象データカテゴリの影響率）
affected_data_ratio = COUNT(data_categories_affected ∩ company_data_usage) / COUNT(company_data_usage)
revenue_at_risk_jpy = data_monetization_revenue_annual_jpy × affected_data_ratio

-- 総財務影響
total_financial_exposure_jpy = max_penalty_jpy + revenue_at_risk_jpy
```

### Step 3: 対応準備度の評価

```
maturity_score:
  advanced = 0.9   -- 高い準拠体制あり（影響を90%緩和）
  standard = 0.6
  basic = 0.3
  none = 0.0

certification_bonus = MIN(0.2, COUNT(existing_certifications) × 0.05)
preparedness = maturity_score + certification_bonus  -- MAX 1.0

vulnerability = 1.0 - MIN(preparedness, 1.0)
```

### Step 4: 時間的切迫度の評価

```
IF enforcement_timeline_months <= 6 THEN urgency = "critical"
ELSE IF enforcement_timeline_months <= 12 THEN urgency = "high"
ELSE IF enforcement_timeline_months <= 24 THEN urgency = "medium"
ELSE urgency = "low"

urgency_weight:
  critical = 1.0
  high = 0.75
  medium = 0.5
  low = 0.25
```

### Step 5: パートナーエコシステム影響の評価

```
-- データ共有先への波及影響
IF data_sharing_partner_count >= 20 THEN ecosystem_risk = "high"
ELSE IF data_sharing_partner_count >= 5 THEN ecosystem_risk = "medium"
ELSE ecosystem_risk = "low"

ecosystem_weight:
  high = 0.3
  medium = 0.15
  low = 0.05
```

### Step 6: 総合インパクトスコア算出

```
financial_factor = MIN(1.0, total_financial_exposure_jpy / (total_annual_revenue_jpy × 0.05))
-- 年間売上高の5%を超える場合に最大値1.0

raw_score = (financial_factor × 0.35 + vulnerability × 0.25 + urgency_weight × 0.25 + ecosystem_weight × 0.15) × 100
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
| `applicability` | enum | 規制の適用可能性 |
| `max_penalty_estimate_jpy` | int | 最大制裁金推定額（円） |
| `revenue_at_risk_jpy` | int | リスクにさらされる収益額（円） |
| `urgency_level` | enum | 時間的切迫度 |
| `recommended_actions` | list[string] | 推奨アクションリスト |
| `risk_summary` | string | リスクサマリー文（自然言語） |

### 推奨アクション分岐

| レベル | 推奨アクション |
|---|---|
| critical | プライバシー対応専任チームの緊急設置、全データ処理プロセスのDSA準拠ギャップ分析実施、同意管理基盤（CMP）の緊急導入/刷新、データ提供パートナー契約の一時凍結検討、経営層・法務部門へのエスカレーション |
| high | データ利活用ポリシーの全面改定着手、ユーザー同意取得フローの再設計、プロファイリング機能の段階的制限計画策定、コンプライアンス要員の増員計画 |
| medium | 規制動向のモニタリング体制構築、現行データ処理の棚卸し・文書化、プライバシー影響評価（PIA）の実施計画策定 |
| low | 業界団体を通じた情報収集、次回定例会議での情報共有、社内教育プログラムへの追加 |

## 参照データソース（Fabric テーブル）

| テーブル名 | 説明 | 主要カラム |
|---|---|---|
| `fact_data_usage_events` | データ利活用イベント | `event_date`, `data_category`, `purpose`, `partner_id`, `user_segment`, `consent_status` |
| `fact_ad_revenue` | 広告・データ収益実績 | `revenue_date`, `revenue_type`, `partner_id`, `amount_jpy`, `data_dependency_level` |
| `dim_data_partners` | データ提供先パートナーマスタ | `partner_id`, `partner_name`, `data_categories_shared`, `contract_expiry`, `jurisdiction` |
| `dim_consent_records` | ユーザー同意管理記録 | `customer_id`, `consent_type`, `consent_status`, `granted_at`, `revoked_at` |
| `fact_user_geography` | ユーザー所在地域情報 | `customer_id`, `country_code`, `region`, `is_eu_resident`, `last_updated` |
| `dim_compliance_certifications` | コンプライアンス認証マスタ | `certification_id`, `certification_name`, `scope`, `valid_from`, `valid_to` |
| `fact_privacy_incidents` | プライバシーインシデント履歴 | `incident_id`, `incident_date`, `severity`, `data_category`, `affected_users`, `resolution_status` |
| `agg_data_revenue_monthly` | 月次データ収益集計 | `month`, `revenue_type`, `total_amount_jpy`, `affected_data_ratio`, `partner_count` |
