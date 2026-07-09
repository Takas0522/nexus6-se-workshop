# SNS事業：EU DSA個人データ規制強化インパクト判断スキル

## 概要

EUデジタルサービス法（DSA）による個人データ規制強化がSNS事業に与えるインパクトを判断するスキル。広告ターゲティングの制限による収益減少、コンテンツモデレーション義務の強化、未成年保護要件への対応コスト、および違反時の制裁金リスクを定量的に評価し、ビジネスモデル転換の緊急度と推奨アクションを出力する。

## 入力パラメータ

### ニュース情報

| パラメータ | 型 | 説明 |
|---|---|---|
| `news_category` | string | ニュースカテゴリ（`regulation_compliance`） |
| `regulation_name` | string | 規制名称（例: `EU_DSA`, `DMA`, `ePrivacy`） |
| `regulation_scope` | enum | `extraterritorial` / `domestic` / `sector_specific` |
| `penalty_rate_percent` | float | 制裁金率（グローバル売上高に対する%） |
| `enforcement_timeline_months` | int | 施行までの猶予期間（月） |
| `targeting_restriction_level` | enum | `full_ban` / `opt_in_only` / `category_restricted` / `disclosure_only` |
| `minor_protection_requirements` | list[string] | 未成年保護要件（例: `profiling_ban`, `age_verification`, `parental_consent`） |
| `transparency_obligations` | list[string] | 透明性義務（例: `algorithmic_disclosure`, `ad_repository`, `quarterly_report`） |

### 業務データ

| パラメータ | 型 | 説明 |
|---|---|---|
| `annual_ad_revenue_jpy` | int | 年間広告収益（円） |
| `targeted_ad_revenue_ratio` | float | ターゲティング広告が広告収益に占める割合（0.0〜1.0） |
| `total_annual_revenue_jpy` | int | SNS事業年間総売上高（円） |
| `eu_user_ratio` | float | EU域内ユーザー比率（0.0〜1.0） |
| `minor_user_ratio` | float | 未成年ユーザー比率（0.0〜1.0） |
| `mau` | int | 月次アクティブユーザー数 |
| `avg_cpm_targeted_jpy` | float | ターゲティング広告の平均CPM（円） |
| `avg_cpm_contextual_jpy` | float | コンテキスト広告の平均CPM（円） |
| `subscription_revenue_ratio` | float | サブスクリプション収益比率（0.0〜1.0） |
| `content_moderation_staff_count` | int | コンテンツモデレーション要員数 |
| `current_consent_opt_in_rate` | float | 現行オプトイン同意率（0.0〜1.0） |
| `data_processing_systems_count` | int | 個人データ処理システム数 |

## 判断ロジック

### Step 1: 広告収益への直接影響算出

```
-- ターゲティング制限による CPM 低下の推定
cpm_decline_ratio:
  full_ban = 1.0           -- ターゲティング広告の完全禁止
  opt_in_only = (1.0 - current_consent_opt_in_rate)  -- オプトイン率分だけ配信可能
  category_restricted = 0.4  -- 一部カテゴリのみ制限
  disclosure_only = 0.1     -- 開示義務のみ（実質的影響小）

-- ターゲティング→コンテキスト広告への強制移行による単価低下
cpm_gap_ratio = (avg_cpm_targeted_jpy - avg_cpm_contextual_jpy) / avg_cpm_targeted_jpy
effective_revenue_decline = targeted_ad_revenue_ratio × cpm_decline_ratio × cpm_gap_ratio

annual_ad_revenue_loss_jpy = annual_ad_revenue_jpy × effective_revenue_decline
```

### Step 2: 制裁金リスクの算出

```
-- 最大制裁金額
max_penalty_jpy = total_annual_revenue_jpy × (penalty_rate_percent / 100)

-- 違反確率の推定（現行対応レベルに基づく）
IF current_consent_opt_in_rate >= 0.8 AND content_moderation_staff_count >= 50 THEN
    violation_probability = 0.1
ELSE IF current_consent_opt_in_rate >= 0.5 THEN
    violation_probability = 0.3
ELSE
    violation_probability = 0.6

expected_penalty_jpy = max_penalty_jpy × violation_probability
```

### Step 3: 未成年保護対応コストの算出

```
minor_user_count = mau × minor_user_ratio

-- 要件別対応コスト
age_verification_cost_jpy = 0
parental_consent_cost_jpy = 0
profiling_ban_cost_jpy = 0

FOR requirement IN minor_protection_requirements:
    IF requirement == "age_verification" THEN
        age_verification_cost_jpy = 500_000_000 + (minor_user_count × 50)  -- 基盤構築 + ユーザーあたり
    ELSE IF requirement == "parental_consent" THEN
        parental_consent_cost_jpy = 300_000_000 + (minor_user_count × 30)
    ELSE IF requirement == "profiling_ban" THEN
        -- 未成年向け広告収益の喪失
        profiling_ban_cost_jpy = annual_ad_revenue_jpy × minor_user_ratio × targeted_ad_revenue_ratio

total_minor_protection_cost_jpy = age_verification_cost_jpy + parental_consent_cost_jpy + profiling_ban_cost_jpy
```

### Step 4: コンテンツモデレーション・透明性対応コスト

```
-- 透明性義務への対応コスト
transparency_cost_per_obligation_jpy = 200_000_000  -- 義務1件あたり年間2億円
total_transparency_cost_jpy = COUNT(transparency_obligations) × transparency_cost_per_obligation_jpy

-- コンテンツモデレーション強化コスト（要員増＋AIシステム）
required_additional_moderators = MAX(0, (mau / 100_000) - content_moderation_staff_count)
moderation_scaling_cost_jpy = required_additional_moderators × 8_000_000  -- 人件費年800万円/人
ai_moderation_system_cost_jpy = 1_000_000_000  -- AI基盤構築10億円（初期）

total_compliance_operational_cost_jpy = total_transparency_cost_jpy + moderation_scaling_cost_jpy + ai_moderation_system_cost_jpy
```

### Step 5: ビジネスモデル耐性の評価

```
-- 広告依存度（高いほど脆弱）
ad_dependency = (annual_ad_revenue_jpy / total_annual_revenue_jpy)

-- 収益多角化による緩和
IF subscription_revenue_ratio >= 0.3 THEN diversification_buffer = 0.6
ELSE IF subscription_revenue_ratio >= 0.15 THEN diversification_buffer = 0.4
ELSE IF subscription_revenue_ratio >= 0.05 THEN diversification_buffer = 0.2
ELSE diversification_buffer = 0.0

business_model_vulnerability = ad_dependency × (1.0 - diversification_buffer)
```

### Step 6: 時間的切迫度

```
-- システム改修に必要な推定期間
estimated_compliance_months = data_processing_systems_count × 2 + COUNT(minor_protection_requirements) × 4
time_margin = enforcement_timeline_months - estimated_compliance_months

IF time_margin < 0 THEN time_pressure = "critical"
ELSE IF time_margin < 6 THEN time_pressure = "high"
ELSE IF time_margin < 12 THEN time_pressure = "medium"
ELSE time_pressure = "low"

time_weight:
  critical = 1.0
  high = 0.75
  medium = 0.45
  low = 0.2
```

### Step 7: 総合インパクトスコア算出

```
-- 財務影響の正規化
total_financial_impact = annual_ad_revenue_loss_jpy + expected_penalty_jpy + total_minor_protection_cost_jpy + total_compliance_operational_cost_jpy
financial_impact_ratio = MIN(1.0, total_financial_impact / (total_annual_revenue_jpy × 0.15))

raw_score = (financial_impact_ratio × 0.35 + business_model_vulnerability × 0.25 + time_weight × 0.25 + (eu_user_ratio + minor_user_ratio) × 0.15) × 100
impact_score = clamp(raw_score, 0, 100)
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
| `annual_ad_revenue_loss_jpy` | int | 年間広告収益損失推定額（円） |
| `max_penalty_jpy` | int | 最大制裁金額（円） |
| `total_compliance_cost_jpy` | int | コンプライアンス対応総コスト（円） |
| `business_model_vulnerability` | float | ビジネスモデル脆弱度（0.0〜1.0） |
| `time_pressure_level` | enum | 時間的切迫度 |
| `recommended_actions` | list[string] | 推奨アクションリスト |
| `risk_summary` | string | リスクサマリー文（自然言語） |

### 推奨アクション分岐

| レベル | 推奨アクション |
|---|---|
| critical | 広告ターゲティングエンジンのDSA準拠改修プロジェクト緊急立ち上げ、コンテキスト広告技術への投資加速（AI文脈理解エンジン）、サブスクリプションモデルへの収益転換ロードマップ策定、未成年年齢確認システムの即時導入決定、EU法務専門チームの設置、経営層への収益構造転換提言 |
| high | 同意管理基盤（CMP）の刷新・多言語対応、広告主向けDSA準拠メニューの設計、コンテンツモデレーションAIの精度向上投資、透明性レポート自動生成システムの構築、プレミアムプラン（広告非表示）の価格・機能設計 |
| medium | DSA準拠ギャップ分析の実施、データ処理目録の作成・文書化、業界団体との連携による規制当局対話、段階的なオプトイン率向上施策のテスト |
| low | 規制動向のモニタリング継続、プライバシーポリシーの定期レビュー、社内教育プログラムの更新 |

## 参照データソース（Fabric テーブル）

| テーブル名 | 説明 | 主要カラム |
|---|---|---|
| `fact_ad_revenue_daily` | 日次広告収益実績 | `date`, `ad_type`, `targeting_method`, `impressions`, `revenue_jpy`, `cpm`, `advertiser_id` |
| `fact_user_consent` | ユーザー同意イベント | `user_id`, `consent_type`, `action`, `timestamp`, `platform`, `jurisdiction` |
| `fact_content_moderation` | コンテンツモデレーション実績 | `date`, `action_type`, `content_category`, `automated_flag`, `human_review`, `resolution` |
| `dim_user_demographics` | ユーザーデモグラフィックマスタ | `user_id`, `age_group`, `is_minor`, `country_code`, `is_eu_resident`, `account_tier` |
| `fact_targeting_events` | ターゲティング配信イベント | `event_date`, `user_id`, `data_categories_used`, `ad_slot_id`, `consent_basis`, `opt_in_status` |
| `dim_ad_inventory` | 広告枠マスタ | `ad_slot_id`, `slot_type`, `targeting_capability`, `avg_cpm_targeted`, `avg_cpm_contextual` |
| `fact_subscription_revenue` | サブスクリプション収益実績 | `date`, `user_id`, `plan_code`, `amount_jpy`, `billing_cycle` |
| `agg_monthly_revenue_mix` | 月次収益構成集計 | `month`, `revenue_source`, `amount_jpy`, `ratio`, `yoy_growth` |
