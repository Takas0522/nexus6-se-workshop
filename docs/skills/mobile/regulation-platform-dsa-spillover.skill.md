# 携帯電話事業：プラットフォーム規制波及インパクト判断スキル

## 概要

EUデジタルサービス法（DSA）等のプラットフォーム規制強化が携帯電話事業に間接的に与えるインパクトを判断するスキル。ターゲティング広告規制に伴う自社マーケティング手法への制約、顧客データ提供・活用の法的リスク、国内規制への波及による事前対応の必要性を評価し、データ利活用戦略の見直し要否と推奨アクションを出力する。

## 入力パラメータ

### ニュース情報

| パラメータ | 型 | 説明 |
|---|---|---|
| `news_category` | string | ニュースカテゴリ（`regulation`） |
| `regulation_name` | string | 規制名称（例: `EU_DSA`, `ePrivacy`, `改正個人情報保護法`） |
| `regulation_jurisdiction` | enum | `eu` / `us` / `japan` / `other` |
| `regulation_scope` | enum | `extraterritorial` / `domestic` / `sector_specific` |
| `affected_platforms` | list[string] | 直接規制対象プラットフォーム（例: `Meta`, `TikTok`, `Google`） |
| `restriction_type` | list[enum] | 規制内容種別: `targeting_ad_ban`, `algorithm_transparency`, `minor_protection`, `data_portability`, `consent_requirement` |
| `penalty_rate_percent` | float | 売上高に対する最大制裁金率（%） |
| `enforcement_timeline_days` | int | 遵守期限（日） |
| `domestic_spillover_likelihood` | enum | `confirmed` / `under_review` / `anticipated` / `unlikely` |

### 業務データ

| パラメータ | 型 | 説明 |
|---|---|---|
| `total_subscriber_count` | int | 総契約者数 |
| `minor_subscriber_count` | int | 18歳未満の契約者数（キッズプラン含む） |
| `ad_revenue_annual_jpy` | int | 広告関連収益（年間・円）※パートナー連携含む |
| `total_annual_revenue_jpy` | int | 携帯電話事業年間売上高（円） |
| `data_sharing_partner_count` | int | データ提供先パートナー企業数 |
| `platform_ad_spend_annual_jpy` | int | 自社がプラットフォーム広告に支出する年間額（円） |
| `targeting_ad_dependency_percent` | float | マーケティング施策のうちターゲティング広告依存率（%） |
| `customer_data_categories` | list[string] | 保有データ種別（例: `location`, `usage_pattern`, `age`, `browsing`, `purchase`） |
| `consent_management_status` | enum | `gdpr_compliant` / `domestic_compliant` / `basic` / `none` |
| `privacy_team_headcount` | int | プライバシー・コンプライアンス担当要員数 |
| `contracts_with_affected_platforms` | list[string] | 規制対象プラットフォームとの契約種別（例: `ad_distribution`, `data_exchange`, `app_preinstall`） |

## 判断ロジック

### Step 1: 自社への適用可能性の判定

```
-- 直接規制対象か間接影響かを判定
IF "carrier_service" IN affected_platforms THEN
    applicability = "direct"
ELSE IF contracts_with_affected_platforms IS NOT EMPTY THEN
    applicability = "contractual"    -- 契約関係を通じた間接影響
ELSE IF domestic_spillover_likelihood IN ("confirmed", "under_review") THEN
    applicability = "anticipated"    -- 国内規制波及が見込まれる
ELSE
    applicability = "indirect"       -- 間接的影響のみ

-- 通信事業者固有の考慮事項
IF regulation_jurisdiction == "eu" AND "data_exchange" IN contracts_with_affected_platforms THEN
    applicability = MAX(applicability, "contractual")
    -- EU域内ユーザーデータのやり取りがある場合は域外適用の可能性
```

### Step 2: マーケティング施策への影響評価

```
-- ターゲティング広告規制の自社マーケティングへの影響
IF "targeting_ad_ban" IN restriction_type THEN
    -- 自社の広告出稿がプラットフォーム規制の影響を受ける度合い
    ad_impact_ratio = targeting_ad_dependency_percent / 100
    affected_ad_spend = platform_ad_spend_annual_jpy × ad_impact_ratio
    
    IF affected_ad_spend / total_annual_revenue_jpy > 0.02 THEN
        marketing_impact = "high"       -- 売上の2%超の広告費に影響
    ELSE IF affected_ad_spend / total_annual_revenue_jpy > 0.005 THEN
        marketing_impact = "medium"     -- 売上の0.5〜2%
    ELSE
        marketing_impact = "low"
ELSE
    marketing_impact = "minimal"

-- 未成年保護規制による自社施策への波及
IF "minor_protection" IN restriction_type AND minor_subscriber_count > 0 THEN
    minor_ratio = minor_subscriber_count / total_subscriber_count
    IF minor_ratio > 0.10 THEN
        minor_impact = "high"           -- 未成年比率10%超
    ELSE IF minor_ratio > 0.03 THEN
        minor_impact = "medium"
    ELSE
        minor_impact = "low"
ELSE
    minor_impact = "none"
```

### Step 3: データ利活用リスクの評価

```
-- 保有データと規制対象データの重複度
regulated_data_overlap = INTERSECTION(customer_data_categories, 
    ["browsing", "usage_pattern", "location", "purchase", "age"])
overlap_ratio = LEN(regulated_data_overlap) / LEN(customer_data_categories)

-- パートナーデータ共有のリスク
IF data_sharing_partner_count > 0 AND overlap_ratio > 0.5 THEN
    data_risk = "high"          -- 規制対象データを多数のパートナーに提供
ELSE IF data_sharing_partner_count > 0 AND overlap_ratio > 0.2 THEN
    data_risk = "medium"
ELSE IF data_sharing_partner_count > 0 THEN
    data_risk = "low"
ELSE
    data_risk = "minimal"

-- 同意管理の成熟度による緩和
IF consent_management_status == "gdpr_compliant" THEN
    data_risk = LOWER(data_risk, 1)     -- 1段階緩和
ELSE IF consent_management_status == "none" THEN
    data_risk = HIGHER(data_risk, 1)    -- 1段階引き上げ
```

### Step 4: 国内規制波及の影響予測

```
-- 日本の個人情報保護委員会等の動きを考慮
IF domestic_spillover_likelihood == "confirmed" THEN
    domestic_timeline_months = 12       -- 1年以内に国内規制化
    domestic_impact_multiplier = 2.0
ELSE IF domestic_spillover_likelihood == "under_review" THEN
    domestic_timeline_months = 24       -- 2年以内の可能性
    domestic_impact_multiplier = 1.5
ELSE IF domestic_spillover_likelihood == "anticipated" THEN
    domestic_timeline_months = 36
    domestic_impact_multiplier = 1.2
ELSE
    domestic_timeline_months = NULL
    domestic_impact_multiplier = 1.0

-- 国内対応の準備度
IF consent_management_status IN ("gdpr_compliant", "domestic_compliant") THEN
    readiness_score = 0.8
ELSE IF consent_management_status == "basic" THEN
    readiness_score = 0.4
ELSE
    readiness_score = 0.1
```

### Step 5: 契約関係への影響評価

```
-- 規制対象プラットフォームとの契約リスク
contract_risk_score = 0

FOR EACH contract_type IN contracts_with_affected_platforms:
    IF contract_type == "data_exchange" THEN
        contract_risk_score += 30       -- データ交換契約は最もリスク大
    ELSE IF contract_type == "ad_distribution" THEN
        contract_risk_score += 20       -- 広告配信連携
    ELSE IF contract_type == "app_preinstall" THEN
        contract_risk_score += 10       -- アプリプリインストール契約
    ELSE
        contract_risk_score += 5

IF contract_risk_score > 50 THEN contract_impact = "high"
ELSE IF contract_risk_score > 20 THEN contract_impact = "medium"
ELSE contract_impact = "low"
```

### Step 6: 総合インパクトスコアの算出

```
-- 基礎スコア算出
applicability_score = {"direct": 30, "contractual": 22, "anticipated": 15, "indirect": 5}[applicability]
marketing_score = {"high": 20, "medium": 12, "low": 5, "minimal": 2}[marketing_impact]
data_risk_score = {"high": 20, "medium": 12, "low": 5, "minimal": 2}[data_risk]
minor_score = {"high": 15, "medium": 8, "low": 3, "none": 0}[minor_impact]
contract_score = {"high": 15, "medium": 8, "low": 3}[contract_impact]

-- 国内波及を考慮した最終スコア
base_score = applicability_score + marketing_score + data_risk_score + minor_score + contract_score
total_impact_score = MIN(base_score × domestic_impact_multiplier, 100)

-- インパクトレベルの決定
IF total_impact_score >= 75 THEN impact_level = "critical"
ELSE IF total_impact_score >= 55 THEN impact_level = "high"
ELSE IF total_impact_score >= 35 THEN impact_level = "medium"
ELSE impact_level = "low"

-- 確信度（applicabilityとデータ充足度に依存）
confidence = 0.5 + (0.3 IF applicability IN ("direct", "contractual") ELSE 0.1) + (readiness_score × 0.2)
```

## 出力

### インパクト評価結果

| 出力項目 | 型 | 説明 |
|---|---|---|
| `impact_level` | enum | `critical` / `high` / `medium` / `low` |
| `impact_score` | int | 総合インパクトスコア（0-100） |
| `confidence_score` | float | 分析確信度（0.00-1.00） |
| `applicability` | enum | 規制の自社適用度 |
| `affected_subscriber_count` | int | 影響を受ける契約者数 |
| `affected_ad_spend_jpy` | int | 影響を受ける広告費（円/年） |
| `domestic_regulation_timeline_months` | int | 国内規制化までの予測期間（月） |
| `data_sharing_risk_partners` | int | データ共有リスクのあるパートナー数 |
| `compliance_gap_severity` | enum | 現行体制と規制要件のギャップ深刻度 |

### 推奨アクション

| 優先度 | アクション | 条件 |
|---|---|---|
| P0（即時） | 規制対象プラットフォームとの既存データ交換契約の法的リスク精査 | `contract_impact` が `high` |
| P0（即時） | 未成年契約者のデータ利用状況の緊急棚卸し | `minor_impact` が `high` かつ `domestic_spillover_likelihood` が `confirmed` |
| P1（1ヶ月以内） | プラットフォーム広告依存度の低減計画策定（ファーストパーティーデータ活用への移行） | `marketing_impact` が `medium` 以上 |
| P1（1ヶ月以内） | パートナー企業へのデータ提供契約の見直し・同意取得状況の確認 | `data_risk` が `medium` 以上 |
| P2（3ヶ月以内） | 同意管理プラットフォーム（CMP）の導入・高度化 | `consent_management_status` が `basic` 以下 |
| P2（3ヶ月以内） | 国内規制動向のモニタリング体制構築（個人情報保護委員会検討会のウォッチ） | `domestic_spillover_likelihood` が `under_review` 以上 |
| P3（6ヶ月以内） | プライバシー・バイ・デザインに基づく新サービス設計ガイドラインの策定 | 常時実行 |
| P3（6ヶ月以内） | コンテキスト広告・オウンドメディア強化等の代替マーケティング手法の試行 | `targeting_ad_dependency_percent` が 40% 以上 |

### 通知テンプレート

```
【{priority}】EUプラットフォーム規制強化に伴う携帯電話事業への影響分析

■ インパクトレベル: {impact_level}（スコア: {impact_score}/100）
■ 適用可能性: {applicability_description}
■ 影響広告費: {affected_ad_spend_jpy:,}円/年
■ 国内規制化予測: {domestic_regulation_timeline_months}ヶ月以内

EUデジタルサービス法の規制強化により、{affected_platforms}に対し
ターゲティング広告制限およびアルゴリズム透明性義務が課されました。

当社への影響:
- マーケティング施策への影響: {marketing_impact}
- データ利活用リスク: {data_risk}
- 未成年契約者関連: {minor_impact}

日本の個人情報保護委員会も同様の規制を検討中であり、
国内規制化に先行した対応が推奨されます。

【推奨アクション】
{recommended_actions}

詳細は添付の分析レポートをご確認ください。
```

## 参照データソース

### Fabric テーブル（sqldb_mobile_01）

| テーブル名 | 用途 |
|---|---|
| `customers` | 契約者の年齢分布・未成年比率の集計、顧客属性の把握 |
| `contracts` | キッズプラン・法人プラン等の契約種別分析、プラットフォーム連携サービスの特定 |
| `transactions` | 広告関連取引の抽出、パートナー連携トランザクションの特定 |
| `inventory` | プリインストールアプリ搭載端末の在庫状況確認 |

### Fabric テーブル（sqldb_common_01）

| テーブル名 | 用途 |
|---|---|
| `unified_customers` | 統合顧客ベースでの未成年・年齢層別影響分析 |
| `customer_segments` | リスクスコア・LTVに基づくデータ利活用対象顧客の特定 |
| `domain_id_mappings` | SNS事業との重複顧客の特定（クロスドメイン影響分析） |

### Fabric テーブル（sqldb_sns_01）

| テーブル名 | 用途 |
|---|---|
| `customers` | SNS事業側の規制対応状況との整合性確認 |
| `contracts` | 広告キャンペーン契約の規制影響範囲特定 |
| `ad_inventory` | ターゲティング広告枠の在庫・依存度分析 |

### Fabric テーブル（sqldb_news_01）

| テーブル名 | 用途 |
|---|---|
| `news_articles` | 関連規制ニュースの時系列追跡、国内検討状況の把握 |
| `impact_analyses` | 過去のDSA関連分析結果の参照、トレンド把握 |
| `notifications` | 過去通知の既読状況確認、重複通知の抑制 |

### 外部参照データ

| データソース | 用途 |
|---|---|
| 欧州委員会 DSA Transparency Database | 規制対象プラットフォームの対応状況・タイムライン |
| 個人情報保護委員会 検討会議事録 | 国内規制の方向性・スケジュール感 |
| Sensor Tower 広告市場レポート | 広告市場規模・手法変化のベンチマーク |
