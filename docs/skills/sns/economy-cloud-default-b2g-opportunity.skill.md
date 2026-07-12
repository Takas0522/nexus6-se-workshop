# SNS事業：官公庁クラウド移行方針に伴うSNSプラットフォーム基盤・B2G事業機会インパクト判断スキル

## 概要

経済産業省「クラウド・バイ・デフォルト2.0」による官公庁IT調達のクラウド前提化が、SNS事業に与えるインパクトを判断するスキル。主に以下の3軸で影響を評価する:（1）官公庁・自治体のSNS公式アカウント利用拡大とAPI連携需要、（2）ISMAP認定取得による自社プラットフォームの信頼性向上・法人利用促進、（3）ゼロトラスト要件によるセキュリティ基盤の見直しコスト。SNS事業にとっては間接的だが、B2G（Business to Government）領域の中期的な拡大機会として評価する。

## 入力パラメータ

### ニュース情報

| パラメータ | 型 | 説明 |
|---|---|---|
| `news_category` | string | ニュースカテゴリ（`economy`） |
| `policy_name` | string | 政策名称（例: `クラウド・バイ・デフォルト2.0`） |
| `cloud_requirement_percent` | int | クラウド構築義務比率（%） |
| `annual_procurement_cases` | int | 年間対象調達案件数 |
| `annual_procurement_amount_jpy` | int | 年間調達総額（円） |
| `budget_effective_fiscal_year` | int | 適用開始年度 |
| `ismap_certified_providers` | list[string] | ISMAP認定クラウド事業者 |
| `zero_trust_required` | bool | ゼロトラスト導入必須か |
| `current_cloud_migration_rate_percent` | float | 現行クラウド移行率（%） |
| `target_cloud_migration_rate_percent` | float | 目標クラウド移行率（%） |

### 業務データ

| パラメータ | 型 | 説明 |
|---|---|---|
| `total_mau` | int | 月間アクティブユーザー数 |
| `government_official_accounts` | int | 官公庁・自治体の公式アカウント数 |
| `government_account_growth_rate_percent` | float | 官公庁アカウントの前年比増加率（%） |
| `api_partner_count` | int | API連携パートナー数 |
| `government_api_integrations` | int | 官公庁向けAPI連携数 |
| `b2g_revenue_monthly_jpy` | int | B2G（行政向け）月間収益（円） |
| `enterprise_plan_subscribers` | int | 法人プラン契約数 |
| `platform_hosting_provider` | string | 自社プラットフォームのホスティング先（例: `AWS`, `GCP`, `on-premise`） |
| `ismap_certified` | bool | 自社がISMAP認定取得済みか |
| `zero_trust_implementation_level` | string | ゼロトラスト実装レベル（`full` / `partial` / `planned` / `none`） |
| `sso_saml_supported` | bool | SSO/SAML認証対応有無 |
| `data_residency_japan` | bool | データの国内保存対応有無 |
| `security_certifications` | list[string] | セキュリティ認証（例: `["ISMS", "SOC2", "ISMAP"]`） |
| `disaster_recovery_rto_hours` | int | DR目標復旧時間（時間） |
| `monthly_infra_cost_jpy` | int | 月間インフラコスト（円） |

## 判断ロジック

### Step 1: B2G事業機会の規模評価

```
-- 官公庁のSNS活用拡大による直接的な事業機会
-- クラウド移行に伴い、行政サービスのデジタルチャネルとしてSNS活用が加速する仮説

-- 現行B2G収益の成長ポテンシャル
IF government_official_accounts >= 500 THEN
    gov_presence = "established"         -- 官公庁利用が定着
    base_growth_potential = 1.5          -- 50%成長可能性
ELSE IF government_official_accounts >= 100 THEN
    gov_presence = "growing"
    base_growth_potential = 2.0          -- 倍増可能性（伸びしろ大）
ELSE IF government_official_accounts >= 20 THEN
    gov_presence = "emerging"
    base_growth_potential = 3.0          -- 3倍可能性（未開拓市場）
ELSE
    gov_presence = "minimal"
    base_growth_potential = 1.2          -- 基盤不足で短期的な成長限定

-- クラウド移行による行政DXの加速度（SNSチャネル利用への間接効果）
cloud_acceleration_factor = (target_cloud_migration_rate_percent - current_cloud_migration_rate_percent) / 100
gov_sns_demand_increase = base_growth_potential × (1 + cloud_acceleration_factor)

-- 市場規模の推計（行政向けSNS関連サービスの潜在市場）
-- 年間調達額のうちSNS/コミュニケーション関連は約0.5%と仮定
potential_gov_sns_market_jpy = annual_procurement_amount_jpy × 0.005
projected_b2g_revenue_annual = b2g_revenue_monthly_jpy × 12 × gov_sns_demand_increase
```

### Step 2: API連携・プラットフォーム需要の評価

```
-- 官公庁クラウド移行に伴うAPI連携需要
-- 行政システムのクラウド化 → 外部サービスとのAPI連携が標準化 → SNS連携需要増

IF government_api_integrations >= 30 THEN
    api_readiness = "mature"
    api_expansion_potential = "incremental"    -- 追加連携は限定的
ELSE IF government_api_integrations >= 10 THEN
    api_readiness = "developing"
    api_expansion_potential = "moderate"       -- 2-3倍の拡大余地
ELSE IF government_api_integrations >= 1 THEN
    api_readiness = "early"
    api_expansion_potential = "high"           -- 大幅拡大余地
ELSE
    api_readiness = "none"
    api_expansion_potential = "greenfield"     -- 新規参入機会

-- API連携の具体的ユースケース
-- 1. 防災情報のSNS自動配信
-- 2. 行政手続きのSNS通知
-- 3. 市民参加型アンケート・パブリックコメント
-- 4. デジタル広報のSNS配信
estimated_new_api_integrations = CASE api_expansion_potential
    WHEN "greenfield" THEN annual_procurement_cases × 0.01    -- 調達案件の1%がSNS連携を含む
    WHEN "high" THEN annual_procurement_cases × 0.008
    WHEN "moderate" THEN annual_procurement_cases × 0.005
    ELSE annual_procurement_cases × 0.002
END
```

### Step 3: セキュリティ・認証要件の対応評価

```
-- ゼロトラスト要件への対応コスト
IF zero_trust_required THEN
    IF zero_trust_implementation_level == "full" THEN
        zt_readiness = "compliant"
        zt_upgrade_cost_jpy = 0
    ELSE IF zero_trust_implementation_level == "partial" THEN
        zt_readiness = "partial"
        zt_upgrade_cost_jpy = 40_000_000       -- 残り部分の実装
    ELSE IF zero_trust_implementation_level == "planned" THEN
        zt_readiness = "planned"
        zt_upgrade_cost_jpy = 100_000_000      -- 計画の前倒し実行
    ELSE
        zt_readiness = "gap"
        zt_upgrade_cost_jpy = 200_000_000      -- ゼロからの構築

-- ISMAP認定の取得評価
IF ismap_certified THEN
    ismap_status = "certified"
    ismap_cost_jpy = 5_000_000                 -- 維持費用のみ
ELSE
    IF "SOC2" IN security_certifications AND "ISMS" IN security_certifications THEN
        ismap_status = "ready_to_apply"
        ismap_cost_jpy = 30_000_000            -- 追加監査+申請
    ELSE IF "ISMS" IN security_certifications THEN
        ismap_status = "partial_ready"
        ismap_cost_jpy = 60_000_000            -- SOC2取得+ISMAP申請
    ELSE
        ismap_status = "not_ready"
        ismap_cost_jpy = 120_000_000           -- ISMS+SOC2+ISMAP

-- データレジデンシー要件
IF NOT data_residency_japan THEN
    data_residency_cost_jpy = 80_000_000       -- 国内データセンター移行
    data_residency_gap = true
ELSE
    data_residency_cost_jpy = 0
    data_residency_gap = false

-- SSO/SAML対応（行政機関の統合認証基盤との連携必須）
IF NOT sso_saml_supported THEN
    sso_implementation_cost_jpy = 25_000_000
ELSE
    sso_implementation_cost_jpy = 0

total_security_cost = zt_upgrade_cost_jpy + ismap_cost_jpy + data_residency_cost_jpy + sso_implementation_cost_jpy
```

### Step 4: インフラコスト影響の評価

```
-- クラウド前提方針による自社インフラへの影響
-- 官公庁要件に合わせたインフラ強化の必要性

-- DR要件の充足（官公庁は通常4時間以内のRTOを要求）
IF disaster_recovery_rto_hours <= 4 THEN
    dr_readiness = "compliant"
    dr_upgrade_cost_jpy = 0
ELSE IF disaster_recovery_rto_hours <= 8 THEN
    dr_readiness = "near_compliant"
    dr_upgrade_cost_jpy = 15_000_000
ELSE
    dr_readiness = "gap"
    dr_upgrade_cost_jpy = 50_000_000

-- B2G対応のための専用テナント/環境構築コスト
IF gov_presence IN ("established", "growing") THEN
    gov_dedicated_env_needed = true
    gov_env_monthly_cost_jpy = monthly_infra_cost_jpy × 0.05   -- インフラの5%を行政専用に
ELSE
    gov_dedicated_env_needed = false
    gov_env_monthly_cost_jpy = 0

annual_additional_infra_cost = (dr_upgrade_cost_jpy + gov_env_monthly_cost_jpy × 12)
```

### Step 5: 競争環境と法人利用の波及効果

```
-- 官公庁利用の「お墨付き効果」による法人利用の拡大
-- 行政機関が利用 → セキュリティ信頼性の証明 → 民間法人も採用加速

IF ismap_certified OR ismap_status == "certified" THEN
    trust_signal_strength = "strong"
    enterprise_growth_multiplier = 1.3   -- 法人契約30%増加のポテンシャル
ELSE IF ismap_status == "ready_to_apply" THEN
    trust_signal_strength = "moderate"
    enterprise_growth_multiplier = 1.15
ELSE
    trust_signal_strength = "weak"
    enterprise_growth_multiplier = 1.05

-- 法人プラン契約の成長予測
projected_enterprise_subscribers = enterprise_plan_subscribers × enterprise_growth_multiplier
enterprise_revenue_uplift = (projected_enterprise_subscribers - enterprise_plan_subscribers) × b2g_revenue_monthly_jpy / enterprise_plan_subscribers × 12

-- 競合プラットフォームとの比較優位
IF ismap_certified AND data_residency_japan AND sso_saml_supported THEN
    competitive_position = "leader"          -- 行政対応では先行
ELSE IF (ismap_certified OR data_residency_japan) AND sso_saml_supported THEN
    competitive_position = "contender"
ELSE
    competitive_position = "lagging"
```

### Step 6: 総合インパクトスコアの算出

```
-- 機会スコア
b2g_opportunity_score = CASE
    WHEN projected_b2g_revenue_annual > b2g_revenue_monthly_jpy × 24 THEN 20  -- 年収が2倍超
    WHEN projected_b2g_revenue_annual > b2g_revenue_monthly_jpy × 18 THEN 15
    WHEN projected_b2g_revenue_annual > b2g_revenue_monthly_jpy × 14 THEN 10
    ELSE 5
END

api_opportunity_score = CASE
    WHEN api_expansion_potential == "greenfield" THEN 15
    WHEN api_expansion_potential == "high" THEN 12
    WHEN api_expansion_potential == "moderate" THEN 8
    ELSE 4
END

enterprise_spillover_score = CASE
    WHEN trust_signal_strength == "strong" THEN 15
    WHEN trust_signal_strength == "moderate" THEN 10
    ELSE 5
END

-- コスト/リスクスコア
security_cost_score = CASE
    WHEN total_security_cost > 200_000_000 THEN 20
    WHEN total_security_cost > 100_000_000 THEN 15
    WHEN total_security_cost > 50_000_000 THEN 10
    ELSE 4
END

infra_cost_score = CASE
    WHEN annual_additional_infra_cost > 80_000_000 THEN 10
    WHEN annual_additional_infra_cost > 30_000_000 THEN 7
    ELSE 3
END

-- 総合スコア（機会ベース+コスト考慮）
opportunity_total = b2g_opportunity_score + api_opportunity_score + enterprise_spillover_score
cost_total = security_cost_score + infra_cost_score

-- SNS事業にとっては間接的影響のため、機会とコストのバランスで判定
total_impact_score = opportunity_total + cost_total
total_impact_score = MIN(total_impact_score, 100)

-- インパクトレベルの決定
IF total_impact_score >= 65 THEN impact_level = "high"
ELSE IF total_impact_score >= 45 THEN impact_level = "medium"
ELSE IF total_impact_score >= 25 THEN impact_level = "low"
ELSE impact_level = "minimal"

-- インパクト種別
IF opportunity_total > cost_total × 1.5 THEN
    impact_type = "opportunity"
ELSE IF cost_total > opportunity_total × 1.5 THEN
    impact_type = "risk"
ELSE
    impact_type = "mixed"
```

## 出力

### インパクト評価結果

| 出力項目 | 型 | 説明 |
|---|---|---|
| `impact_level` | enum | `high` / `medium` / `low` / `minimal` |
| `impact_score` | int | 総合インパクトスコア（0-100） |
| `confidence_score` | float | 分析確信度（0.00-1.00） |
| `impact_type` | enum | `opportunity` / `risk` / `mixed` |
| `projected_b2g_revenue_annual_jpy` | int | B2G年間収益予測（円） |
| `total_security_cost_jpy` | int | セキュリティ対応コスト合計（円） |
| `gov_presence` | string | 官公庁利用の現状 |
| `api_expansion_potential` | string | API連携拡大ポテンシャル |
| `ismap_status` | string | ISMAP認定の状況 |
| `zt_readiness` | string | ゼロトラスト対応状況 |
| `competitive_position` | string | 行政対応における競合上の位置付け |
| `enterprise_growth_multiplier` | float | 法人契約成長倍率 |
| `potential_gov_sns_market_jpy` | int | 行政SNS関連潜在市場規模（円） |

### 推奨アクション

| 優先度 | アクション | 条件 |
|---|---|---|
| P1（1ヶ月以内） | ISMAP認定取得プロジェクトの立ち上げ | `ismap_status` が `not_ready` または `partial_ready` |
| P1（1ヶ月以内） | 官公庁向けAPI連携パッケージの企画策定 | `api_expansion_potential` が `high` または `greenfield` |
| P2（3ヶ月以内） | ゼロトラストアーキテクチャへの移行計画策定 | `zt_readiness` が `gap` または `planned` |
| P2（3ヶ月以内） | 行政機関向け専用プラン（SSO/監査ログ/SLA保証）の設計 | `gov_presence` が `growing` 以上 |
| P2（3ヶ月以内） | データレジデンシー（国内保存）対応の実施 | `data_residency_gap` が `true` |
| P2（3ヶ月以内） | 防災・行政広報向けSNS配信APIの開発・ドキュメント整備 | `government_api_integrations` が 10 未満 |
| P3（6ヶ月以内） | 自治体DX推進部門への営業チーム組成・アプローチ開始 | `gov_presence` が `emerging` または `minimal` |
| P3（6ヶ月以内） | 法人プランのセキュリティ強化（監査証跡・アクセス制御の高度化） | `enterprise_growth_multiplier` が 1.2 以上 |
| P3（6ヶ月以内） | 「行政機関利用実績」を活用したB2Bマーケティング施策の展開 | `trust_signal_strength` が `strong` |

### 通知テンプレート

```
【{priority}】官公庁クラウド移行方針「クラウド・バイ・デフォルト2.0」のSNS事業への影響

■ インパクトレベル: {impact_level}（スコア: {impact_score}/100）
■ インパクト種別: {impact_type}
■ B2G年間収益予測: {projected_b2g_revenue_annual_jpy:,}円
■ セキュリティ対応コスト: {total_security_cost_jpy:,}円
■ 行政SNS潜在市場: {potential_gov_sns_market_jpy:,}円

経済産業省が「クラウド・バイ・デフォルト2.0」を策定し、
官公庁IT調達の{cloud_requirement_percent}%をクラウド前提とする方針が公表されました。

【事業機会】
- 官公庁アカウント利用状況: {gov_presence}（現行{government_official_accounts}件）
- API連携拡大ポテンシャル: {api_expansion_potential}
- 法人契約への波及効果: {enterprise_growth_multiplier:.2f}x
- 競合ポジション: {competitive_position}

【対応課題】
- ISMAP認定: {ismap_status}
- ゼロトラスト実装: {zt_readiness}
- データ国内保存: {data_residency_gap}

【推奨アクション】
{recommended_actions}

本方針は直接的なSNSサービスへの義務ではありませんが、
行政DX加速による中期的なB2G市場拡大の機会として注視を推奨します。
```

## 参照データソース

### Fabric テーブル（sqldb_sns_01）

| テーブル名 | 用途 |
|---|---|
| `customers` | 官公庁・自治体アカウントの識別（account_tier=`government`等）、法人アカウント数の集計 |
| `contracts` | 法人プラン（enterprise/government）の契約数・収益分析 |
| `transactions` | B2G関連収益の内訳分析、API利用料の集計 |
| `ad_inventory` | 行政広報向け広告枠の提供状況 |

### Fabric テーブル（sqldb_si_01）

| テーブル名 | 用途 |
|---|---|
| `customers` | 官公庁顧客のIT調達動向との突合（SI事業の官公庁案件からの示唆） |
| `contracts` | クラウド移行プロジェクトにおけるSNS連携要件の把握 |

### Fabric テーブル（sqldb_common_01）

| テーブル名 | 用途 |
|---|---|
| `unified_customers` | 官公庁顧客の事業領域横断的な利用実態把握 |
| `domain_id_mappings` | 官公庁がSNS+SI+モバイルを併用しているケースの特定 |

### Fabric テーブル（sqldb_news_01）

| テーブル名 | 用途 |
|---|---|
| `news_articles` | クラウド政策・行政DX関連ニュースの追跡 |
| `impact_analyses` | SI事業での同一ニュース分析結果（官公庁クラウド案件の動向）との統合 |
| `notifications` | 過去の関連通知との整合・重複排除 |

### 外部参照データ

| データソース | 用途 |
|---|---|
| ISMAP クラウドサービスリスト | 認定事業者・サービスの最新状況 |
| デジタル庁 行政DXレポート | 官公庁のデジタルサービス利用動向 |
| 総務省 自治体DX推進計画 | 地方自治体のSNS公式アカウント利用方針 |
