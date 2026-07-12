# SI事業：官公庁クラウド移行案件の大規模受注機会インパクト判断スキル

## 概要

経済産業省「クラウド・バイ・デフォルト2.0」による官公庁IT調達のクラウド前提化（年間8,200件・1兆2,000億円規模）が、SI事業にもたらすクラウド移行・ゼロトラスト構築・ISMAP対応案件の受注機会を判断するスキル。SI事業にとって**最もインパクトの大きいシナリオ**であり、（1）既存オンプレミスシステムのクラウドリフト&シフト案件、（2）クラウドネイティブ新規構築案件、（3）ゼロトラストアーキテクチャ設計・導入、（4）ISMAP認定支援コンサルティング——の4軸で事業機会を評価する。

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
| `target_fiscal_year` | int | 目標達成年度 |

### 業務データ

| パラメータ | 型 | 説明 |
|---|---|---|
| `total_active_projects` | int | 現在進行中のプロジェクト数 |
| `government_sector_clients` | int | 官公庁・独法の顧客数 |
| `government_sector_revenue_ratio_percent` | float | 官公庁売上比率（%） |
| `cloud_migration_projects_count` | int | クラウド移行プロジェクト実績数 |
| `cloud_native_projects_count` | int | クラウドネイティブ開発プロジェクト実績数 |
| `zero_trust_projects_count` | int | ゼロトラスト導入プロジェクト実績数 |
| `ismap_consulting_experience` | bool | ISMAP認定支援コンサルの経験有無 |
| `aws_certified_engineers` | int | AWS認定エンジニア数 |
| `azure_certified_engineers` | int | Azure認定エンジニア数 |
| `gcp_certified_engineers` | int | GCP認定エンジニア数 |
| `security_engineers_count` | int | セキュリティエンジニア数 |
| `total_engineering_staff` | int | エンジニア総数 |
| `available_engineers` | int | アサイン可能エンジニア数 |
| `partner_network_count` | int | 協力会社・パートナー数 |
| `cloud_partner_certifications` | list[string] | クラウドパートナー認定（例: `["AWS Advanced", "Azure Gold", "GCP Premier"]`） |
| `avg_project_monthly_revenue_jpy` | int | プロジェクト平均月間収益（円） |
| `annual_revenue_jpy` | int | SI事業年間売上（円） |
| `current_utilization_rate_percent` | float | 現在の稼働率（%） |
| `government_procurement_registered` | bool | 政府調達登録の有無 |
| `security_clearance_staff` | int | セキュリティクリアランス保有者数 |

## 判断ロジック

### Step 1: 対象市場の規模と自社アクセス可能市場の算出

```
-- 年間調達総額のうちSI/インテグレーション領域の推定
-- クラウドサービス利用料: 30%
-- SI/構築・移行費用: 45%（←SI事業の主戦場）
-- コンサルティング: 10%
-- 運用・保守: 15%
si_market_ratio = 0.45
consulting_market_ratio = 0.10
operations_market_ratio = 0.15

-- クラウド前提案件のみ（全体の70%）
cloud_procurement_amount = annual_procurement_amount_jpy × (cloud_requirement_percent / 100)
si_total_market_jpy = cloud_procurement_amount × si_market_ratio
consulting_total_market_jpy = cloud_procurement_amount × consulting_market_ratio
operations_total_market_jpy = cloud_procurement_amount × operations_market_ratio
total_si_addressable_market_jpy = si_total_market_jpy + consulting_total_market_jpy + operations_total_market_jpy

-- 未移行分の追加市場（既存オンプレからの移行案件）
migration_gap_percent = target_cloud_migration_rate_percent - current_cloud_migration_rate_percent
-- 移行対象システム数 = 調達案件数 × 移行ギャップ率
migration_target_systems = annual_procurement_cases × (migration_gap_percent / 100)
-- 移行案件の平均単価（新規構築の60%と仮定）
avg_migration_project_value = cloud_procurement_amount / annual_procurement_cases × 0.6
migration_market_jpy = migration_target_systems × avg_migration_project_value

-- 合計市場
total_market_jpy = total_si_addressable_market_jpy + migration_market_jpy

-- 自社のシェア推定（官公庁との取引実績ベース）
IF government_sector_clients >= 20 THEN
    gov_market_position = "major_player"
    estimated_share_percent = 5.0         -- 大手SIerとして5%
ELSE IF government_sector_clients >= 10 THEN
    gov_market_position = "established"
    estimated_share_percent = 2.5
ELSE IF government_sector_clients >= 5 THEN
    gov_market_position = "growing"
    estimated_share_percent = 1.2
ELSE IF government_sector_clients >= 1 THEN
    gov_market_position = "entrant"
    estimated_share_percent = 0.5
ELSE
    gov_market_position = "new"
    estimated_share_percent = 0.1

base_expected_revenue_jpy = total_market_jpy × (estimated_share_percent / 100)
```

### Step 2: クラウド技術力による競争力の加減算

```
-- クラウドエンジニアの充足度
total_cloud_certified = aws_certified_engineers + azure_certified_engineers + gcp_certified_engineers
cloud_engineer_ratio = total_cloud_certified / total_engineering_staff

IF cloud_engineer_ratio >= 0.4 THEN
    cloud_capability = "industry_leading"
    capability_multiplier = 1.5
ELSE IF cloud_engineer_ratio >= 0.25 THEN
    cloud_capability = "strong"
    capability_multiplier = 1.3
ELSE IF cloud_engineer_ratio >= 0.15 THEN
    cloud_capability = "adequate"
    capability_multiplier = 1.1
ELSE IF cloud_engineer_ratio >= 0.08 THEN
    cloud_capability = "developing"
    capability_multiplier = 0.9
ELSE
    cloud_capability = "insufficient"
    capability_multiplier = 0.6

-- マルチクラウド対応力（官公庁はベンダーロックイン回避を重視）
clouds_covered = 0
IF aws_certified_engineers >= 5 THEN clouds_covered += 1
IF azure_certified_engineers >= 5 THEN clouds_covered += 1
IF gcp_certified_engineers >= 5 THEN clouds_covered += 1

IF clouds_covered >= 3 THEN
    multicloud_capability = "full"
    multicloud_bonus = 1.2
ELSE IF clouds_covered >= 2 THEN
    multicloud_capability = "dual"
    multicloud_bonus = 1.1
ELSE
    multicloud_capability = "single"
    multicloud_bonus = 1.0

-- ゼロトラスト対応力
IF zero_trust_required THEN
    IF zero_trust_projects_count >= 5 THEN
        zt_capability = "expert"
        zt_premium = 1.15                 -- ゼロトラスト案件の追加単価
    ELSE IF zero_trust_projects_count >= 2 THEN
        zt_capability = "experienced"
        zt_premium = 1.08
    ELSE IF zero_trust_projects_count >= 1 THEN
        zt_capability = "developing"
        zt_premium = 1.0
    ELSE
        zt_capability = "gap"
        zt_premium = 0.85                 -- ゼロトラスト要件に対応できずシェア縮小
ELSE
    zt_capability = "not_required"
    zt_premium = 1.0

-- クラウドパートナー認定による優位性
partner_tier_bonus = 0
IF "AWS Advanced" IN cloud_partner_certifications OR "AWS Premier" IN cloud_partner_certifications THEN
    partner_tier_bonus += 0.05
IF "Azure Gold" IN cloud_partner_certifications OR "Azure Solutions" IN cloud_partner_certifications THEN
    partner_tier_bonus += 0.05
IF "GCP Premier" IN cloud_partner_certifications THEN
    partner_tier_bonus += 0.05

-- 調整後の期待受注額
adjusted_expected_revenue_jpy = base_expected_revenue_jpy × capability_multiplier × multicloud_bonus × zt_premium × (1 + partner_tier_bonus)
```

### Step 3: 案件カテゴリ別の機会配分

```
-- 4つの案件カテゴリへの配分

-- 1. リフト&シフト（既存オンプレ→クラウド移行）
IF cloud_migration_projects_count >= 20 THEN
    migration_win_rate = 0.30
ELSE IF cloud_migration_projects_count >= 10 THEN
    migration_win_rate = 0.20
ELSE IF cloud_migration_projects_count >= 5 THEN
    migration_win_rate = 0.12
ELSE
    migration_win_rate = 0.05

migration_revenue = migration_market_jpy × (estimated_share_percent / 100) × (migration_win_rate / 0.15)

-- 2. クラウドネイティブ新規構築
IF cloud_native_projects_count >= 15 THEN
    native_capability = "expert"
    native_revenue_ratio = 0.35
ELSE IF cloud_native_projects_count >= 8 THEN
    native_capability = "strong"
    native_revenue_ratio = 0.28
ELSE IF cloud_native_projects_count >= 3 THEN
    native_capability = "developing"
    native_revenue_ratio = 0.18
ELSE
    native_capability = "novice"
    native_revenue_ratio = 0.08

native_revenue = adjusted_expected_revenue_jpy × native_revenue_ratio

-- 3. ゼロトラスト設計・導入
IF zero_trust_required THEN
    zt_market_share_of_total = 0.20       -- 全体の20%がゼロトラスト関連
    zt_revenue = adjusted_expected_revenue_jpy × zt_market_share_of_total × zt_premium
ELSE
    zt_revenue = 0

-- 4. ISMAP認定支援コンサルティング
IF ismap_consulting_experience THEN
    ismap_consulting_revenue = consulting_total_market_jpy × 0.03   -- コンサル市場の3%
ELSE
    ismap_consulting_revenue = consulting_total_market_jpy × 0.005

total_categorized_revenue = migration_revenue + native_revenue + zt_revenue + ismap_consulting_revenue
```

### Step 4: リソースキャパシティと拡張計画

```
-- 想定される案件規模と数
avg_cloud_project_team_size = 8           -- クラウド案件の平均チームサイズ
avg_project_duration_months = 9           -- 平均プロジェクト期間

-- 年間で対応可能な案件数
annual_capacity_projects = (available_engineers / avg_cloud_project_team_size) × (12 / avg_project_duration_months)

-- 需要に対するキャパシティの充足度
estimated_annual_projects = adjusted_expected_revenue_jpy / (avg_project_monthly_revenue_jpy × avg_project_duration_months)

IF annual_capacity_projects >= estimated_annual_projects × 1.2 THEN
    capacity_status = "surplus"            -- 余裕あり
    hiring_urgency = "low"
ELSE IF annual_capacity_projects >= estimated_annual_projects THEN
    capacity_status = "balanced"           -- ちょうど足りる
    hiring_urgency = "moderate"
ELSE IF annual_capacity_projects >= estimated_annual_projects × 0.7 THEN
    capacity_status = "stretched"          -- やや不足、パートナー活用必須
    hiring_urgency = "high"
ELSE
    capacity_status = "critical_shortage"  -- 大幅不足
    hiring_urgency = "critical"

-- パートナー活用による補完
IF partner_network_count >= 30 THEN
    partner_augmentation = "strong"
    effective_capacity_multiplier = 1.8    -- パートナー込みで1.8倍の対応力
ELSE IF partner_network_count >= 15 THEN
    partner_augmentation = "moderate"
    effective_capacity_multiplier = 1.4
ELSE IF partner_network_count >= 5 THEN
    partner_augmentation = "limited"
    effective_capacity_multiplier = 1.2
ELSE
    partner_augmentation = "minimal"
    effective_capacity_multiplier = 1.05

effective_annual_capacity = annual_capacity_projects × effective_capacity_multiplier

-- 稼働率の推移予測
projected_utilization = current_utilization_rate_percent + (estimated_annual_projects / effective_annual_capacity × 20)
IF projected_utilization > 95 THEN
    utilization_risk = "overload"
ELSE IF projected_utilization > 85 THEN
    utilization_risk = "high_load"
ELSE
    utilization_risk = "manageable"
```

### Step 5: 政府調達要件への適合度

```
-- 政府調達への参入障壁チェック
barriers_cleared = 0
total_barriers = 5

-- 1. 政府調達登録
IF government_procurement_registered THEN barriers_cleared += 1

-- 2. ISMAP関連の知見
IF ismap_consulting_experience OR "ISMAP" IN cloud_partner_certifications THEN barriers_cleared += 1

-- 3. セキュリティクリアランス
IF security_clearance_staff >= 5 THEN barriers_cleared += 1

-- 4. 官公庁実績
IF government_sector_clients >= 5 THEN barriers_cleared += 1

-- 5. クラウドパートナー認定
IF LEN(cloud_partner_certifications) >= 1 THEN barriers_cleared += 1

barrier_clearance_ratio = barriers_cleared / total_barriers

IF barrier_clearance_ratio >= 0.8 THEN
    procurement_readiness = "fully_qualified"
ELSE IF barrier_clearance_ratio >= 0.6 THEN
    procurement_readiness = "mostly_qualified"
ELSE IF barrier_clearance_ratio >= 0.4 THEN
    procurement_readiness = "partially_qualified"
ELSE
    procurement_readiness = "significant_gaps"

-- 未充足の障壁による参入コスト
unmet_barriers = total_barriers - barriers_cleared
barrier_resolution_cost_jpy = unmet_barriers × 15_000_000    -- 1障壁あたり1,500万円と概算
barrier_resolution_months = unmet_barriers × 4               -- 1障壁あたり4ヶ月
```

### Step 6: 総合インパクトスコアの算出

```
-- 市場規模スコア
market_score = CASE
    WHEN adjusted_expected_revenue_jpy > annual_revenue_jpy × 0.20 THEN 30
    WHEN adjusted_expected_revenue_jpy > annual_revenue_jpy × 0.10 THEN 25
    WHEN adjusted_expected_revenue_jpy > annual_revenue_jpy × 0.05 THEN 18
    WHEN adjusted_expected_revenue_jpy > annual_revenue_jpy × 0.02 THEN 12
    ELSE 6
END

-- クラウド技術力スコア
capability_score = CASE
    WHEN cloud_capability == "industry_leading" THEN 20
    WHEN cloud_capability == "strong" THEN 16
    WHEN cloud_capability == "adequate" THEN 12
    WHEN cloud_capability == "developing" THEN 7
    ELSE 3
END

-- 調達参入資格スコア
procurement_score = CASE
    WHEN procurement_readiness == "fully_qualified" THEN 20
    WHEN procurement_readiness == "mostly_qualified" THEN 15
    WHEN procurement_readiness == "partially_qualified" THEN 9
    ELSE 4
END

-- キャパシティスコア
capacity_score = CASE
    WHEN capacity_status == "surplus" THEN 15
    WHEN capacity_status == "balanced" THEN 12
    WHEN capacity_status == "stretched" THEN 8
    ELSE 4
END

-- ゼロトラスト対応ボーナス/ペナルティ
zt_score = CASE
    WHEN zt_capability == "expert" THEN 10
    WHEN zt_capability == "experienced" THEN 7
    WHEN zt_capability == "developing" THEN 4
    WHEN zt_capability == "gap" THEN -5
    ELSE 0
END

-- 総合スコア
total_impact_score = market_score + capability_score + procurement_score + capacity_score + zt_score
total_impact_score = MAX(MIN(total_impact_score, 100), 0)

-- インパクトレベル（SI事業にとって最大のチャンスのためcritical閾値を設定）
IF total_impact_score >= 75 THEN impact_level = "critical"
ELSE IF total_impact_score >= 60 THEN impact_level = "high"
ELSE IF total_impact_score >= 40 THEN impact_level = "medium"
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
| `total_market_jpy` | int | SI対象市場規模合計（円） |
| `adjusted_expected_revenue_jpy` | int | 調整後期待受注額（円） |
| `migration_revenue_jpy` | int | リフト&シフト案件の期待収益（円） |
| `native_revenue_jpy` | int | クラウドネイティブ案件の期待収益（円） |
| `zt_revenue_jpy` | int | ゼロトラスト案件の期待収益（円） |
| `ismap_consulting_revenue_jpy` | int | ISMAP支援コンサル期待収益（円） |
| `gov_market_position` | string | 官公庁市場での自社ポジション |
| `cloud_capability` | string | クラウド技術力レベル |
| `procurement_readiness` | string | 調達参入資格の充足度 |
| `capacity_status` | string | リソースキャパシティ状況 |
| `zt_capability` | string | ゼロトラスト対応力 |
| `multicloud_capability` | string | マルチクラウド対応力 |
| `estimated_annual_projects` | int | 年間想定プロジェクト数 |
| `hiring_urgency` | string | 採用の緊急度 |

### 推奨アクション

| 優先度 | アクション | 条件 |
|---|---|---|
| P0（即時） | 官公庁既存顧客へのクラウド移行計画ヒアリング・早期提案活動 | `government_sector_clients` が 5 以上 |
| P0（即時） | 2027年度予算に向けた提案活動の前倒し開始 | 常時実行（`budget_effective_fiscal_year` が翌年度） |
| P1（1ヶ月以内） | クラウドエンジニアの大量採用計画策定（AWS/Azure/GCP） | `hiring_urgency` が `high` または `critical` |
| P1（1ヶ月以内） | ゼロトラストアーキテクチャ設計のリファレンス実装・PoC環境構築 | `zt_capability` が `gap` または `developing` |
| P1（1ヶ月以内） | クラウドパートナー認定のアップグレード申請 | `cloud_partner_certifications` が不十分 |
| P2（3ヶ月以内） | ISMAP認定支援コンサルティングサービスの商品化 | `ismap_consulting_experience` が `false` |
| P2（3ヶ月以内） | 官公庁向けクラウド移行方法論・テンプレートの整備 | `cloud_migration_projects_count` が 5 以上 |
| P2（3ヶ月以内） | 政府調達登録・セキュリティクリアランスの取得 | `procurement_readiness` が `partially_qualified` 以下 |
| P2（3ヶ月以内） | パートナー企業とのクラウド案件向けアライアンス強化 | `capacity_status` が `stretched` または `critical_shortage` |
| P3（6ヶ月以内） | マルチクラウド対応の検証ラボ設置（AWS+Azure+GCP+国産） | `multicloud_capability` が `single` |
| P3（6ヶ月以内） | 官公庁クラウド移行のケーススタディ・実績資料の整備・公開 | 常時実行 |
| P3（6ヶ月以内） | 国産クラウド（さくら・IIJ）対応エンジニアの育成 | `ismap_certified_providers` に国産事業者が含まれる場合 |

### 通知テンプレート

```
【{priority}】官公庁「クラウド・バイ・デフォルト2.0」に伴う大規模SI案件機会

■ インパクトレベル: {impact_level}（スコア: {impact_score}/100）
■ インパクト種別: {impact_type}
■ SI対象市場規模: {total_market_jpy:,}円
■ 調整後期待受注額: {adjusted_expected_revenue_jpy:,}円
■ 年間想定案件数: {estimated_annual_projects}件
■ 適用開始: {budget_effective_fiscal_year}年度予算から

経済産業省「クラウド・バイ・デフォルト2.0」により、
官公庁IT調達（年間{annual_procurement_cases:,}件・{annual_procurement_amount_jpy:,}円）の
{cloud_requirement_percent}%がクラウド前提となります。

【案件カテゴリ別の機会】
1. クラウド移行（リフト&シフト）: {migration_revenue_jpy:,}円
2. クラウドネイティブ新規構築: {native_revenue_jpy:,}円
3. ゼロトラスト設計・導入: {zt_revenue_jpy:,}円
4. ISMAP認定支援コンサル: {ismap_consulting_revenue_jpy:,}円

【自社ポジション】
- 官公庁市場地位: {gov_market_position}
- クラウド技術力: {cloud_capability}
- マルチクラウド対応: {multicloud_capability}
- ゼロトラスト: {zt_capability}
- 調達参入資格: {procurement_readiness}

【リソース計画】
- キャパシティ: {capacity_status}
- 採用緊急度: {hiring_urgency}
- パートナー活用: {partner_augmentation}
- 稼働率見込み: {projected_utilization:.1f}%

【推奨アクション】
{recommended_actions}

本方針はSI事業にとって最大級の市場拡大機会です。
FY{budget_effective_fiscal_year}予算編成に向けた早期の提案活動を強く推奨します。
```

## 参照データソース

### Fabric テーブル（sqldb_si_01）

| テーブル名 | 用途 |
|---|---|
| `customers` | 官公庁・独法顧客の特定、業界コード`G`（公務）による抽出、取引規模の分析 |
| `contracts` | クラウド移行・ゼロトラスト関連の過去プロジェクト実績、契約規模・期間の参照 |
| `transactions` | 官公庁案件の収益推移、季節性（年度末集中）の分析、外注費率の把握 |
| `resource_inventory` | クラウド認定エンジニアの在籍・スキル分布、セキュリティエンジニアの稼働状況 |

### Fabric テーブル（sqldb_mobile_01）

| テーブル名 | 用途 |
|---|---|
| `contracts` | 携帯電話事業のクラウド基盤契約（グループ内ナレッジの活用） |
| `inventory` | ネットワーク機器のクラウド化動向（エッジ/クラウド連携の知見） |

### Fabric テーブル（sqldb_sns_01）

| テーブル名 | 用途 |
|---|---|
| `customers` | SNS事業のクラウド基盤利用実績（グループ内リファレンス） |

### Fabric テーブル（sqldb_common_01）

| テーブル名 | 用途 |
|---|---|
| `unified_customers` | 官公庁顧客の事業横断的な取引実態（SI+モバイル+SNSの複合提案の基礎） |
| `domain_id_mappings` | 官公庁が複数事業で取引しているケースの特定（クロスセル機会） |
| `customer_segments` | 官公庁顧客のLTV・成長ポテンシャルの評価 |

### Fabric テーブル（sqldb_news_01）

| テーブル名 | 用途 |
|---|---|
| `news_articles` | クラウド政策・ISMAP・ゼロトラスト関連ニュースの継続追跡 |
| `impact_analyses` | 携帯電話事業・SNS事業での同一ニュース分析結果との統合（複合提案の示唆） |
| `notifications` | 過去のクラウド関連通知との整合、戦略的一貫性の確保 |

### 外部参照データ

| データソース | 用途 |
|---|---|
| ISMAP クラウドサービスリスト | 認定サービス・事業者の最新情報 |
| 経済産業省 IT調達方針ガイドライン | 調達要件の詳細仕様 |
| デジタル庁 政府情報システム管理データベース | 官公庁システムの現行クラウド利用状況 |
| 各クラウドベンダー パートナープログラム | パートナー認定要件・特典情報 |
| 内閣サイバーセキュリティセンター（NISC） | ゼロトラスト要件の技術仕様 |
