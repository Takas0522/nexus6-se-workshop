# SNS事業：改正個人情報保護法インパクト判断スキル

## 前提

- 本文書は nexus6-se-workshop の Demo 用ナレッジであり、数値・事例はすべて想定値である。
- 個人情報、実在企業の機密、実顧客の行動履歴は含めない。
- 対象事業部は SNS事業 である。
- 対応シナリオは「改正個人情報保護法によるデータ規制強化（法規制・コンプライアンスリスク）」。
- Agent 2 は影響有無の評価、Agent 3 は事業部別 Next Action の草案作成に使う。

## 概要

2026年施行の改正個人情報保護法により、ユーザー行動データの第三者提供およびターゲティング広告への利用に本人の明示的同意（オプトイン）が必須化されたことが、SNS事業の広告収益モデル・ユーザーデータ活用基盤・広告主リテンションに与えるインパクトを判断するスキル。同意取得率に基づく広告配信可能オーディエンスの縮小、広告単価への影響、制裁金リスク、および広告主離反リスクを定量評価し、推奨アクションを出力する。

## 適用シナリオ

- 外部ニュースのトリガー: 改正法の施行発表、ガイドライン公布、同業他社への制裁金事例報道、EU/GDPR連動規制の報道。
- SNS事業はユーザー行動データを広告ターゲティングに活用する収益モデルの中核であり、法改正の影響を最も直接的に受ける事業領域。
- 広告収益への影響は `sns_transactions`（AD_REVENUE）と `sns_ad_campaigns` のデータで定量評価する。
- 有料サブスクリプション（`sns_subscriptions`）はデータ規制の直接影響が小さいが、広告からの転換先として評価する。

## 入力パラメータ

### ニュース情報

| パラメータ | 型 | 説明 |
|---|---|---|
| `news_category` | string | ニュースカテゴリ（`data_privacy_regulation`） |
| `regulation_name` | string | 法令名（例: `改正個人情報保護法`） |
| `enforcement_date` | date | 施行日 |
| `consent_requirement` | string | 同意要件（例: `explicit_optin`, `opt_out`, `legitimate_interest`） |
| `third_party_sharing_restricted` | bool | 第三者提供制限の有無 |
| `targeting_ad_restricted` | bool | ターゲティング広告制限の有無 |
| `max_penalty_pct_of_revenue` | float | 最大制裁金率（売上高比、例: 4.0） |
| `transition_period_months` | int | 経過措置期間（月） |
| `cross_border_transfer_restricted` | bool | 越境データ移転の制限有無 |

### 業務データ

| パラメータ | 型 | 説明 | 参照テーブル |
|---|---|---|---|
| `total_active_users` | int | アクティブユーザー総数 | `sns_users` (status=ACTIVE) |
| `current_consent_rate` | float | 現在の明示的同意取得率（0.0〜1.0） | CMP（同意管理基盤） |
| `behavioral_data_users` | int | 行動データ取得中のユーザー数 | データ基盤 |
| `monthly_ad_revenue_jpy` | int | 月間広告収益（円） | `sns_transactions` (type=AD_REVENUE) |
| `targeted_ad_revenue_ratio` | float | 広告収益に占めるターゲティング広告の割合（0.0〜1.0） | `sns_transactions` 分析 |
| `active_ad_campaigns` | int | 稼働中の広告キャンペーン数 | `sns_ad_campaigns` (status=ACTIVE) |
| `avg_campaign_budget_jpy` | int | 広告キャンペーン平均予算（円） | `sns_ad_campaigns` 集計 |
| `advertiser_count` | int | アクティブ広告主数 | `sns_ad_campaigns` 集計 |
| `subscription_revenue_jpy` | int | 月間サブスクリプション収益（円） | `sns_subscriptions` + `sns_transactions` |
| `subscription_conversion_rate` | float | 無料→有料転換率（月次、0.0〜1.0） | `sns_subscriptions` 分析 |
| `annual_revenue_jpy` | int | 年間総売上高（円） | 財務データ |
| `privacy_team_headcount` | int | プライバシー・法務チーム人数 | 組織情報 |
| `cmp_implementation_status` | enum | CMP導入状況: `deployed`/`partial`/`planned`/`none` | システム台帳 |
| `data_processing_agreements_count` | int | 締結済みデータ処理契約（DPA）数 | 法務管理 |

## 判断ロジック

### Step 1: 同意ギャップの評価

```
-- 必要同意率（規制が求める水準）
IF consent_requirement == "explicit_optin" THEN
  required_consent_rate = 1.0
ELSE IF consent_requirement == "opt_out" THEN
  required_consent_rate = 0.0  -- オプトアウト方式ならギャップなし
ELSE
  required_consent_rate = 0.5

consent_gap = MAX(0, required_consent_rate - current_consent_rate)

-- 同意取得可能ユーザー推定
-- 業界実績：オプトイン要請時の同意率は既存ユーザーで 40-60%
estimated_post_regulation_consent_rate = MIN(current_consent_rate + 0.15, 0.55)
targetable_users_after = total_active_users × estimated_post_regulation_consent_rate
audience_shrinkage_pct = (1.0 - estimated_post_regulation_consent_rate) × 100
```

### Step 2: 広告収益への影響算出

```
-- ターゲティング広告収益の損失推定
IF targeting_ad_restricted THEN
  targetable_revenue = monthly_ad_revenue_jpy × targeted_ad_revenue_ratio
  non_targetable_revenue = monthly_ad_revenue_jpy × (1.0 - targeted_ad_revenue_ratio)

  -- 同意済みユーザーへのターゲティングは維持
  retained_targeted_revenue = targetable_revenue × estimated_post_regulation_consent_rate

  -- 非同意ユーザーへはコンテキスト広告に転換（CPM 50%低下を想定）
  contextual_fallback_revenue = targetable_revenue × (1.0 - estimated_post_regulation_consent_rate) × 0.50

  projected_ad_revenue = retained_targeted_revenue + contextual_fallback_revenue + non_targetable_revenue
  monthly_revenue_loss = monthly_ad_revenue_jpy - projected_ad_revenue
  revenue_loss_pct = (monthly_revenue_loss / monthly_ad_revenue_jpy) × 100
ELSE
  monthly_revenue_loss = 0
  revenue_loss_pct = 0.0

-- 6ヶ月累積損失
six_month_revenue_loss = monthly_revenue_loss × 6
```

### Step 3: 広告主離反リスクの評価

```
-- 広告効果低下による広告主離反率推定
IF revenue_loss_pct >= 30 THEN
  advertiser_churn_risk = "critical"
  estimated_advertiser_churn_pct = 25.0
ELSE IF revenue_loss_pct >= 20 THEN
  advertiser_churn_risk = "high"
  estimated_advertiser_churn_pct = 15.0
ELSE IF revenue_loss_pct >= 10 THEN
  advertiser_churn_risk = "medium"
  estimated_advertiser_churn_pct = 8.0
ELSE
  advertiser_churn_risk = "low"
  estimated_advertiser_churn_pct = 3.0

at_risk_campaigns = active_ad_campaigns × (estimated_advertiser_churn_pct / 100)
at_risk_budget_jpy = at_risk_campaigns × avg_campaign_budget_jpy
```

### Step 4: 制裁金リスクの評価

```
max_penalty_jpy = annual_revenue_jpy × (max_penalty_pct_of_revenue / 100)

-- 準備状況による違反リスク
IF cmp_implementation_status == "deployed" AND current_consent_rate >= 0.7 THEN
  violation_probability = "low"
  expected_penalty_jpy = max_penalty_jpy × 0.05
ELSE IF cmp_implementation_status == "deployed" THEN
  violation_probability = "medium"
  expected_penalty_jpy = max_penalty_jpy × 0.15
ELSE IF cmp_implementation_status == "partial" THEN
  violation_probability = "high"
  expected_penalty_jpy = max_penalty_jpy × 0.30
ELSE
  violation_probability = "critical"
  expected_penalty_jpy = max_penalty_jpy × 0.50
```

### Step 5: 準備度（レディネス）の評価

```
readiness_score = 0

-- CMP導入状況
IF cmp_implementation_status == "deployed" THEN readiness_score += 30
ELSE IF cmp_implementation_status == "partial" THEN readiness_score += 15
ELSE IF cmp_implementation_status == "planned" THEN readiness_score += 5

-- 同意取得率
readiness_score += MIN(25, current_consent_rate × 25)

-- 法務チーム体制
IF privacy_team_headcount >= 10 THEN readiness_score += 20
ELSE IF privacy_team_headcount >= 5 THEN readiness_score += 12
ELSE IF privacy_team_headcount >= 2 THEN readiness_score += 5

-- DPA整備率
dpa_coverage = data_processing_agreements_count / MAX(1, advertiser_count)
readiness_score += MIN(15, dpa_coverage × 15)

-- 施行までの残り時間
days_to_enforcement = (enforcement_date - TODAY).days
IF days_to_enforcement > 365 THEN readiness_score += 10
ELSE IF days_to_enforcement > 180 THEN readiness_score += 5
ELSE IF days_to_enforcement <= 90 THEN readiness_score -= 10

readiness_level:
  readiness_score >= 70 → "sufficient"
  readiness_score >= 45 → "partial"
  readiness_score >= 20 → "insufficient"
  OTHERWISE → "critical_gap"
```

### Step 6: 第三者提供・越境データへの影響

```
IF third_party_sharing_restricted THEN
  -- 外部DSP/DMPとのデータ連携が制限される
  third_party_data_impact = "high"
  external_data_enrichment_loss = TRUE
ELSE
  third_party_data_impact = "low"
  external_data_enrichment_loss = FALSE

IF cross_border_transfer_restricted THEN
  -- グローバル広告ネットワークとの連携に影響
  cross_border_impact = "high"
ELSE
  cross_border_impact = "low"
```

### Step 7: 総合インパクトスコア算出

```
revenue_impact_weight = MIN(1.0, revenue_loss_pct / 35.0)

advertiser_churn_weight:
  critical → 1.0
  high     → 0.7
  medium   → 0.4
  low      → 0.1

penalty_weight:
  critical → 1.0
  high     → 0.7
  medium   → 0.4
  low      → 0.1

readiness_penalty:
  critical_gap  → 0.3
  insufficient  → 0.15
  partial       → 0.05
  sufficient    → 0.0

raw_score = (revenue_impact_weight × 0.30
           + advertiser_churn_weight × 0.20
           + penalty_weight × 0.20
           + consent_gap × 0.15
           + readiness_penalty × 0.15)
           × 100

-- 第三者提供・越境移転の加算
IF third_party_data_impact == "high" THEN raw_score += 5
IF cross_border_impact == "high" THEN raw_score += 5

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
| `consent_gap` | float | 同意ギャップ（0.0〜1.0） |
| `audience_shrinkage_pct` | float | 広告配信可能オーディエンス縮小率（%） |
| `monthly_revenue_loss_jpy` | int | 月間広告収益損失推定額（円） |
| `six_month_revenue_loss_jpy` | int | 6ヶ月累積損失推定額（円） |
| `advertiser_churn_risk` | enum | 広告主離反リスクレベル |
| `at_risk_campaigns` | int | 離反リスクのあるキャンペーン数 |
| `violation_probability` | enum | 法令違反リスク |
| `expected_penalty_jpy` | int | 制裁金リスク推定額（円） |
| `readiness_level` | enum | 準備度レベル |
| `recommended_actions` | list[string] | 推奨アクションリスト |
| `notification_channel` | string | 推奨通知チャネル |
| `risk_summary` | string | リスクサマリー文（自然言語） |

### 推奨アクション分岐

| レベル | 推奨アクション | 通知チャネル |
|---|---|---|
| critical | CMP（同意管理プラットフォーム）の即時導入または全面改修、全ユーザーへのオプトイン同意取得UXの緊急実装、ターゲティング広告の代替収益モデル（コンテキスト広告・サブスクリプション転換）の設計開始、広告主向け新メニュー（プライバシーファースト広告）の策定、制裁金引当金の計上検討、第三者データ連携の総点検・DPA再締結、経営層・法務部門への即時エスカレーション | slack + email |
| high | 同意取得フローの改善UX設計、コンテキスト広告配信エンジンの開発着手、広告主への影響説明・リテンション施策の準備、プライバシーポリシーの改定作業、DPA未締結の広告主への対応計画策定、サブスクリプションプラン拡充の検討 | slack |
| medium | 同意取得率向上施策の企画（インセンティブ付与等）、広告収益のシナリオ別シミュレーション実施、法務チームによるガイドライン解釈の整理、同業他社の対応事例の調査 | slack |
| low | 法改正動向のモニタリング継続、年次プライバシー影響評価（PIA）の更新、次期予算でのCMP投資計画策定 | 定期レポート |

### 通知テンプレート（critical / high 時）

```
件名: 【{impact_level}】改正個人情報保護法によるSNS広告収益モデルへの重大影響
本文:
改正個人情報保護法の施行に伴い、SNS事業の広告収益モデルに
重大な影響が見込まれます。
- インパクトスコア: {impact_score}/100
- 同意ギャップ: {consent_gap:.0%}
  （現在同意率: {current_consent_rate:.0%}
   → 施行後推定: {estimated_post_regulation_consent_rate:.0%}）
- 広告配信可能オーディエンス縮小: {audience_shrinkage_pct:.1f}%
- 月間広告収益損失推定: ¥{monthly_revenue_loss_jpy:,}
  （6ヶ月累積: ¥{six_month_revenue_loss_jpy:,}）
- 広告主離反リスク: {advertiser_churn_risk}（対象: {at_risk_campaigns}件）
- 制裁金リスク: ¥{expected_penalty_jpy:,}（売上高最大{max_penalty_pct_of_revenue}%）
- 準備度: {readiness_level}

同意取得UXの改善および代替収益モデルの検討を
速やかに開始してください。
```

## 参照データソース（Fabric テーブル）

| テーブル名 | DB | 説明 | 主要カラム |
|---|---|---|---|
| `sns_users` | sqldb_sns_01 | SNS事業のユーザーマスタ | `sns_user_id`, `display_name`, `account_tier`, `status` |
| `sns_subscriptions` | sqldb_sns_01 | サブスクリプション契約情報 | `subscription_id`, `sns_user_id`, `plan_code`, `monthly_fee`, `status` |
| `sns_transactions` | sqldb_sns_01 | 取引履歴 | `transaction_id`, `sns_user_id`, `transaction_type`, `amount`, `transaction_date` |
| `sns_ad_campaigns` | sqldb_sns_01 | 広告キャンペーン管理 | `campaign_id`, `advertiser_id`, `campaign_name`, `budget`, `spent_amount`, `status` |
| `unified_customers` | sqldb_common_01 | 統合顧客マスタ | `unified_customer_id`, `full_name`, `email`, `phone_number` |
| `domain_id_mappings` | sqldb_common_01 | 業務領域IDマッピング | `unified_customer_id`, `domain_code`, `domain_customer_id`, `is_active` |
| `customer_segments` | sqldb_common_01 | 顧客セグメント | `unified_customer_id`, `segment_code`, `risk_score`, `lifetime_value` |

## データ品質メモ

- `sns_transactions` の `transaction_type = 'AD_REVENUE'` を月次集計して `monthly_ad_revenue_jpy` を算出する。ターゲティング広告とコンテキスト広告の内訳は `description` カラムのパターン分析または別途広告管理システムから取得する。
- `sns_ad_campaigns` の `advertiser_id` をDISTINCT集計して `advertiser_count` を算出する。`status = 'ACTIVE'` かつ `end_date >= TODAY` の条件でフィルタすること。
- 同意取得率（`current_consent_rate`）はFabricテーブル外のCMP（同意管理プラットフォーム）から取得する前提。CMP未導入の場合は 0.0 として計算する。
- `sns_subscriptions` の `status = 'ACTIVE'` 件数と `monthly_fee` 合計がサブスクリプション収益の近似値となる。広告収益依存度の算出に使用する（広告収益 / (広告収益 + サブスクリプション収益)）。
- `customer_segments.segment_code = 'PREMIUM'` のユーザーは LTV が高く、同意取得の優先ターゲットとして推奨。PRIMIUMセグメントの同意率を個別にモニタリングすることが望ましい。
- 広告主へのDPA（データ処理契約）締結状況は法務管理システムから取得する前提。Fabricテーブルには格納されていない。
