# SI事業：プラットフォーム規制対応コンサルティング・システム開発案件機会インパクト判断スキル

## 概要

EU デジタルサービス法（DSA）によるMeta・TikTok等への追加義務（アルゴリズム透明性・未成年保護・ターゲティング広告制限）が、SI事業にもたらすコンサルティング・システム開発案件の受注機会を判断するスキル。規制対応が必要なプラットフォーム事業者・広告主・メディア企業に対し、（1）アルゴリズム監査・説明可能AI（XAI）導入支援、（2）年齢確認・未成年保護システム構築、（3）プライバシーコンプライアンス基盤の開発、（4）広告配信システムの改修支援——の4つの案件カテゴリでの事業機会を評価する。

## 入力パラメータ

### ニュース情報

| パラメータ | 型 | 説明 |
|---|---|---|
| `news_category` | string | ニュースカテゴリ（`regulation`） |
| `regulation_name` | string | 規制名称（例: `DSA`） |
| `target_platforms` | list[string] | 規制対象プラットフォーム |
| `compliance_deadline_days` | int | 準拠期限（日数） |
| `penalty_max_revenue_percent` | float | 最大制裁金率（%） |
| `algorithm_transparency_required` | bool | アルゴリズム開示義務の有無 |
| `behavioral_ad_ban_minors` | bool | 未成年行動ターゲティング禁止の有無 |
| `eu_social_ad_market_eur_billion` | float | 欧州SNS広告市場規模（十億EUR） |
| `domestic_regulation_status` | string | 国内同種規制の検討状況 |

### 業務データ

| パラメータ | 型 | 説明 |
|---|---|---|
| `total_active_projects` | int | 現在進行中のプロジェクト数 |
| `media_platform_clients` | int | メディア・プラットフォーム業界の顧客数 |
| `advertising_industry_clients` | int | 広告業界の顧客数 |
| `privacy_compliance_projects_count` | int | プライバシー・コンプライアンス関連プロジェクト実績数 |
| `ai_ml_projects_count` | int | AI/ML関連プロジェクト実績数 |
| `xai_experience` | bool | 説明可能AI（XAI）の開発経験有無 |
| `age_verification_system_experience` | bool | 年齢確認システムの開発経験有無 |
| `ad_tech_projects_count` | int | アドテック関連プロジェクト実績数 |
| `gdpr_compliance_experience` | bool | GDPR対応プロジェクトの経験有無 |
| `data_engineers_count` | int | データエンジニア数 |
| `security_engineers_count` | int | セキュリティエンジニア数 |
| `available_engineers` | int | アサイン可能エンジニア数 |
| `consulting_staff_count` | int | コンサルタント数 |
| `partner_network_count` | int | 協力会社・パートナー数 |
| `avg_project_monthly_revenue_jpy` | int | プロジェクト平均月間収益（円） |
| `annual_revenue_jpy` | int | SI事業年間売上（円） |
| `current_utilization_rate_percent` | float | 現在の稼働率（%） |

## 判断ロジック

### Step 1: 市場機会の規模推定

```
-- DSA規制対応に伴うIT投資の推定
-- 対象企業カテゴリ:
-- A) 大規模プラットフォーム事業者（日本法人含む）
-- B) 国内SNS・メディアプラットフォーム事業者
-- C) 広告主・広告代理店（広告配信の見直し）
-- D) 一般企業（プライバシーコンプライアンス対応）

-- EU規制の国内波及度に基づく市場規模推定
IF domestic_regulation_status == "announced" THEN
    domestic_market_multiplier = 3.0      -- 国内規制も発表済み、市場急拡大
    urgency_factor = "high"
ELSE IF domestic_regulation_status == "under_review" THEN
    domestic_market_multiplier = 1.8      -- 検討中、先行対応需要あり
    urgency_factor = "medium"
ELSE
    domestic_market_multiplier = 1.0      -- 海外対応のみ
    urgency_factor = "low"

-- 案件カテゴリ別の市場規模推定（年間）
-- 1. アルゴリズム監査・XAI導入: プラットフォーム事業者向け
algorithm_audit_market_jpy = 15_000_000_000 × domestic_market_multiplier   -- ベース150億円

-- 2. 年齢確認・未成年保護システム: プラットフォーム+一般事業者
IF behavioral_ad_ban_minors THEN
    minor_protection_market_jpy = 20_000_000_000 × domestic_market_multiplier  -- ベース200億円
ELSE
    minor_protection_market_jpy = 5_000_000_000 × domestic_market_multiplier

-- 3. プライバシーコンプライアンス基盤: 幅広い企業
privacy_compliance_market_jpy = 30_000_000_000 × domestic_market_multiplier    -- ベース300億円

-- 4. 広告配信システム改修: 広告主・代理店
ad_system_revision_market_jpy = 10_000_000_000 × domestic_market_multiplier    -- ベース100億円

total_addressable_market_jpy = algorithm_audit_market_jpy + minor_protection_market_jpy + privacy_compliance_market_jpy + ad_system_revision_market_jpy
```

### Step 2: 自社の参入可能性・競争力評価

```
-- カテゴリ別の自社競争力

-- 1. アルゴリズム監査・XAI
IF xai_experience AND ai_ml_projects_count >= 10 THEN
    algorithm_audit_capability = "strong"
    algorithm_audit_share = 0.03          -- 市場の3%
ELSE IF ai_ml_projects_count >= 5 THEN
    algorithm_audit_capability = "moderate"
    algorithm_audit_share = 0.015
ELSE IF ai_ml_projects_count >= 2 THEN
    algorithm_audit_capability = "developing"
    algorithm_audit_share = 0.008
ELSE
    algorithm_audit_capability = "weak"
    algorithm_audit_share = 0.002

-- 2. 年齢確認・未成年保護
IF age_verification_system_experience THEN
    minor_protection_capability = "strong"
    minor_protection_share = 0.025
ELSE IF security_engineers_count >= 10 THEN
    minor_protection_capability = "moderate"
    minor_protection_share = 0.012
ELSE
    minor_protection_capability = "weak"
    minor_protection_share = 0.005

-- 3. プライバシーコンプライアンス
IF gdpr_compliance_experience AND privacy_compliance_projects_count >= 5 THEN
    privacy_capability = "strong"
    privacy_share = 0.02
ELSE IF privacy_compliance_projects_count >= 2 THEN
    privacy_capability = "moderate"
    privacy_share = 0.01
ELSE
    privacy_capability = "weak"
    privacy_share = 0.004

-- 4. アドテック
IF ad_tech_projects_count >= 5 THEN
    adtech_capability = "strong"
    adtech_share = 0.02
ELSE IF ad_tech_projects_count >= 2 THEN
    adtech_capability = "moderate"
    adtech_share = 0.01
ELSE
    adtech_capability = "weak"
    adtech_share = 0.003

-- 期待受注額の算出
expected_revenue_algorithm = algorithm_audit_market_jpy × algorithm_audit_share
expected_revenue_minor = minor_protection_market_jpy × minor_protection_share
expected_revenue_privacy = privacy_compliance_market_jpy × privacy_share
expected_revenue_adtech = ad_system_revision_market_jpy × adtech_share

total_expected_revenue_jpy = expected_revenue_algorithm + expected_revenue_minor + expected_revenue_privacy + expected_revenue_adtech
```

### Step 3: 既存顧客からの案件発生可能性

```
-- 既存のメディア・プラットフォーム顧客からの直接受注確率
IF media_platform_clients >= 10 THEN
    direct_client_opportunity = "high"
    existing_client_revenue_ratio = 0.6   -- 期待受注の60%は既存顧客から
ELSE IF media_platform_clients >= 5 THEN
    direct_client_opportunity = "moderate"
    existing_client_revenue_ratio = 0.4
ELSE IF media_platform_clients >= 2 THEN
    direct_client_opportunity = "limited"
    existing_client_revenue_ratio = 0.25
ELSE
    direct_client_opportunity = "minimal"
    existing_client_revenue_ratio = 0.1

-- 広告業界顧客からの派生案件
IF advertising_industry_clients >= 5 THEN
    ad_client_opportunity = "high"
ELSE IF advertising_industry_clients >= 2 THEN
    ad_client_opportunity = "moderate"
ELSE
    ad_client_opportunity = "low"

-- コンサルティング起点の案件化（上流からの参入）
IF consulting_staff_count >= 10 THEN
    consulting_led_opportunity = "strong"
    consulting_premium_ratio = 1.3        -- コンサル込みで30%増のプロジェクト単価
ELSE IF consulting_staff_count >= 5 THEN
    consulting_led_opportunity = "moderate"
    consulting_premium_ratio = 1.15
ELSE
    consulting_led_opportunity = "limited"
    consulting_premium_ratio = 1.0
```

### Step 4: タイムラインと緊急度の評価

```
-- 規制準拠期限に基づく案件タイムライン
-- DSA準拠期限90日 → 海外事業者は即座に対応開始
-- 国内事業者は先行的に対応を検討（波及を見越して）

IF compliance_deadline_days <= 90 THEN
    international_urgency = "critical"    -- 海外拠点持つ企業は即対応
    first_rfp_expected_weeks = 2          -- 2週間以内にRFP
ELSE IF compliance_deadline_days <= 180 THEN
    international_urgency = "high"
    first_rfp_expected_weeks = 6
ELSE
    international_urgency = "moderate"
    first_rfp_expected_weeks = 12

-- 国内規制への先行対応タイムライン
IF domestic_regulation_status == "announced" THEN
    domestic_rfp_expected_months = 3      -- 3ヶ月以内に国内案件も発生
ELSE IF domestic_regulation_status == "under_review" THEN
    domestic_rfp_expected_months = 9      -- 先行検討として9ヶ月後
ELSE
    domestic_rfp_expected_months = 18     -- 様子見、18ヶ月後以降

-- 案件のフェーズ展開
-- Phase 1（0-3ヶ月）: アセスメント・コンサルティング案件
-- Phase 2（3-9ヶ月）: システム設計・開発案件
-- Phase 3（9-18ヶ月）: 運用・監査・継続改善案件
phase1_revenue_ratio = 0.2
phase2_revenue_ratio = 0.5
phase3_revenue_ratio = 0.3
```

### Step 5: リソースとデリバリーリスク

```
-- 必要スキルセットの充足度
required_skills = ["AI/ML", "Privacy", "Security", "AdTech"]
skill_coverage = 0
IF ai_ml_projects_count >= 3 THEN skill_coverage += 1
IF privacy_compliance_projects_count >= 2 THEN skill_coverage += 1
IF security_engineers_count >= 5 THEN skill_coverage += 1
IF ad_tech_projects_count >= 2 THEN skill_coverage += 1

skill_coverage_ratio = skill_coverage / 4

IF skill_coverage_ratio >= 0.75 THEN
    delivery_capability = "strong"
ELSE IF skill_coverage_ratio >= 0.5 THEN
    delivery_capability = "adequate"
ELSE IF skill_coverage_ratio >= 0.25 THEN
    delivery_capability = "partial"
ELSE
    delivery_capability = "gap"

-- 人材の確保可能性
estimated_team_size_per_project = 6      -- コンプライアンス案件の平均チームサイズ
max_parallel_projects = available_engineers / estimated_team_size_per_project

IF max_parallel_projects >= 5 THEN
    capacity_status = "ample"
ELSE IF max_parallel_projects >= 3 THEN
    capacity_status = "sufficient"
ELSE IF max_parallel_projects >= 1 THEN
    capacity_status = "limited"
ELSE
    capacity_status = "insufficient"

-- 短期間での立ち上げリスク（90日期限のプレッシャー）
IF first_rfp_expected_weeks <= 4 AND capacity_status IN ("limited", "insufficient") THEN
    rapid_delivery_risk = "high"
ELSE
    rapid_delivery_risk = "manageable"
```

### Step 6: 総合インパクトスコアの算出

```
-- 機会規模スコア
market_score = CASE
    WHEN total_expected_revenue_jpy > annual_revenue_jpy × 0.15 THEN 25
    WHEN total_expected_revenue_jpy > annual_revenue_jpy × 0.08 THEN 20
    WHEN total_expected_revenue_jpy > annual_revenue_jpy × 0.03 THEN 14
    ELSE 7
END

-- 競争力スコア（4カテゴリの平均）
avg_capability_score = (
    {"strong": 5, "moderate": 3, "developing": 2, "weak": 1}[algorithm_audit_capability] +
    {"strong": 5, "moderate": 3, "weak": 1}[minor_protection_capability] +
    {"strong": 5, "moderate": 3, "weak": 1}[privacy_capability] +
    {"strong": 5, "moderate": 3, "weak": 1}[adtech_capability]
) / 4
capability_score = avg_capability_score × 4    -- 最大20点

-- 既存顧客アクセススコア
client_access_score = CASE
    WHEN direct_client_opportunity == "high" THEN 20
    WHEN direct_client_opportunity == "moderate" THEN 14
    WHEN direct_client_opportunity == "limited" THEN 8
    ELSE 4
END

-- 緊急度スコア
urgency_score = CASE
    WHEN international_urgency == "critical" AND domestic_regulation_status == "announced" THEN 20
    WHEN international_urgency == "critical" THEN 15
    WHEN international_urgency == "high" THEN 12
    ELSE 7
END

-- デリバリーリスクの減点
delivery_deduction = CASE
    WHEN delivery_capability == "gap" THEN -8
    WHEN delivery_capability == "partial" THEN -4
    ELSE 0
END

-- 総合スコア
total_impact_score = market_score + capability_score + client_access_score + urgency_score + delivery_deduction
total_impact_score = MAX(MIN(total_impact_score, 100), 0)

-- インパクトレベル
IF total_impact_score >= 70 THEN impact_level = "critical"
ELSE IF total_impact_score >= 55 THEN impact_level = "high"
ELSE IF total_impact_score >= 35 THEN impact_level = "medium"
ELSE IF total_impact_score >= 20 THEN impact_level = "low"
ELSE impact_level = "minimal"

impact_type = "opportunity"
```

## 出力

### インパクト評価結果

| 出力項目 | 型 | 説明 |
|---|---|---|
| `impact_level` | enum | `critical` / `high` / `medium` / `low` / `minimal` |
| `impact_score` | int | 総合インパクトスコア（0-100） |
| `confidence_score` | float | 分析確信度（0.00-1.00） |
| `impact_type` | enum | `opportunity` |
| `total_addressable_market_jpy` | int | 対象市場規模合計（円） |
| `total_expected_revenue_jpy` | int | 期待受注額合計（円） |
| `expected_revenue_by_category` | object | カテゴリ別期待受注額 |
| `direct_client_opportunity` | string | 既存顧客からの案件発生度 |
| `delivery_capability` | string | デリバリー能力 |
| `first_rfp_expected_weeks` | int | 初回RFP発行までの推定期間（週） |
| `domestic_rfp_expected_months` | int | 国内案件発生までの推定期間（月） |
| `max_parallel_projects` | int | 同時対応可能プロジェクト数 |
| `strongest_category` | string | 最も競争力のある案件カテゴリ |

### 推奨アクション

| 優先度 | アクション | 条件 |
|---|---|---|
| P0（即時） | 既存メディア・プラットフォーム顧客へのDSA対応ニーズヒアリング実施 | `media_platform_clients` が 2 以上 |
| P0（即時） | DSA準拠アセスメントサービスの提案書・サービスメニュー策定 | `international_urgency` が `critical` |
| P1（1ヶ月以内） | 説明可能AI（XAI）ソリューションの自社検証・デモ環境構築 | `xai_experience` が `false` AND `ai_ml_projects_count` が 3 以上 |
| P1（1ヶ月以内） | 年齢確認システムのリファレンスアーキテクチャ策定 | `behavioral_ad_ban_minors` が `true` |
| P1（1ヶ月以内） | プライバシーコンプライアンス専門チームの組成 | `privacy_capability` が `moderate` 以上 |
| P2（3ヶ月以内） | 広告業界顧客向けターゲティング広告代替手法のコンサルティング提案 | `advertising_industry_clients` が 2 以上 |
| P2（3ヶ月以内） | GDPR/DSA対応の知見を持つパートナー企業とのアライアンス構築 | `gdpr_compliance_experience` が `false` |
| P2（3ヶ月以内） | AI倫理・アルゴリズム監査の外部認証取得（IEEE等） | `algorithm_audit_capability` が `strong` |
| P3（6ヶ月以内） | 国内個人情報保護委員会の規制動向モニタリングと先行提案の準備 | `domestic_regulation_status` が `under_review` |
| P3（6ヶ月以内） | 規制対応SIのケーススタディ・ホワイトペーパー公開 | 常時実行 |

### 通知テンプレート

```
【{priority}】EU DSA規制強化に伴うSIプロジェクト機会の分析

■ インパクトレベル: {impact_level}（スコア: {impact_score}/100）
■ インパクト種別: {impact_type}
■ 対象市場規模: {total_addressable_market_jpy:,}円
■ 期待受注額: {total_expected_revenue_jpy:,}円
■ 初回RFP見込み: {first_rfp_expected_weeks}週間後
■ 国内案件見込み: {domestic_rfp_expected_months}ヶ月後

EU DSAに基づくMeta・TikTokへの追加義務（{compliance_deadline_days}日以内の準拠）が
発表され、広範なコンプライアンス対応IT投資が見込まれます。

【案件カテゴリ別の機会】
1. アルゴリズム監査・XAI導入: {expected_revenue_algorithm:,}円（能力: {algorithm_audit_capability}）
2. 年齢確認・未成年保護: {expected_revenue_minor:,}円（能力: {minor_protection_capability}）
3. プライバシーコンプライアンス: {expected_revenue_privacy:,}円（能力: {privacy_capability}）
4. 広告配信システム改修: {expected_revenue_adtech:,}円（能力: {adtech_capability}）

【自社ポジション】
- 既存顧客からの案件発生: {direct_client_opportunity}
- デリバリー能力: {delivery_capability}
- 同時対応可能案件数: {max_parallel_projects}件
- 最強カテゴリ: {strongest_category}

【フェーズ展開】
- Phase 1（0-3ヶ月）: アセスメント・コンサル … 売上の20%
- Phase 2（3-9ヶ月）: システム設計・開発 … 売上の50%
- Phase 3（9-18ヶ月）: 運用・監査・改善 … 売上の30%

【推奨アクション】
{recommended_actions}

規制対応は一過性ではなく継続的な案件が見込まれます。
早期のポジション確立を推奨します。
```

## 参照データソース

### Fabric テーブル（sqldb_si_01）

| テーブル名 | 用途 |
|---|---|
| `customers` | メディア・プラットフォーム業界顧客の特定、広告業界顧客の抽出 |
| `contracts` | プライバシー/コンプライアンス/AI関連プロジェクトの実績分析、案件規模の参照 |
| `transactions` | 関連案件の収益推移、業界セクター別の売上分析 |
| `resource_inventory` | AI/MLエンジニア、セキュリティエンジニア、コンサルタントの在籍・スキル分析 |

### Fabric テーブル（sqldb_sns_01）

| テーブル名 | 用途 |
|---|---|
| `customers` | 自社SNS事業のユーザー規模（グループ内での規制対応ニーズの把握） |
| `contracts` | 自社SNS事業の広告契約（グループ内案件としての対応優先度判断） |
| `ad_inventory` | 広告枠のターゲティング手法の実態把握（SI知見の蓄積に活用） |

### Fabric テーブル（sqldb_common_01）

| テーブル名 | 用途 |
|---|---|
| `unified_customers` | メディア・広告業界顧客の事業横断的な関係性把握 |
| `customer_segments` | 規制対応ニーズが高いセグメントの顧客特定 |

### Fabric テーブル（sqldb_news_01）

| テーブル名 | 用途 |
|---|---|
| `news_articles` | プライバシー規制・DSA関連ニュースの継続追跡 |
| `impact_analyses` | SNS事業側の同一ニュース分析結果（グループ内のニーズ把握） |
| `notifications` | 過去の規制関連通知との重複排除 |

### 外部参照データ

| データソース | 用途 |
|---|---|
| 個人情報保護委員会 審議会資料 | 国内プラットフォーム規制の立法動向 |
| European Commission DSA Portal | DSA義務の詳細要件・ガイドライン |
| IDC Japan IT市場予測 | コンプライアンスIT投資の市場規模予測 |
| Gartner Privacy by Design Framework | プライバシー対応のベストプラクティス |
