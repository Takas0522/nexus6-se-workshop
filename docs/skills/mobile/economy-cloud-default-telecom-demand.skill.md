# 携帯電話事業：官公庁クラウド移行に伴う法人通信需要インパクト判断スキル

## 概要

経済産業省「クラウド・バイ・デフォルト2.0」による官公庁IT調達のクラウド前提化が、携帯電話事業の法人向け通信サービスに与えるインパクトを判断するスキル。閉域網接続サービス、SD-WAN、モバイルアクセス回線、IoT回線など法人通信需要の変化を評価し、ビジネス機会の規模と対応の緊急度を出力する。

## 入力パラメータ

### ニュース情報

| パラメータ | 型 | 説明 |
|---|---|---|
| `news_category` | string | ニュースカテゴリ（`economy`） |
| `policy_name` | string | 政策名（例: `クラウド・バイ・デフォルト2.0`） |
| `policy_authority` | string | 策定省庁（例: `経済産業省`） |
| `target_cloud_ratio_percent` | float | クラウド調達目標比率（%） |
| `target_fiscal_year` | int | 適用開始年度（例: `2027`） |
| `annual_procurement_count` | int | 年間対象調達案件数 |
| `annual_procurement_value_jpy` | int | 年間対象調達総額（円） |
| `current_cloud_ratio_percent` | float | 現行クラウド移行率（%） |
| `security_requirements` | list[string] | セキュリティ要件（例: `zero_trust`, `ismap`, `encryption`） |
| `eligible_cloud_providers` | list[string] | 調達対象クラウド事業者 |

### 業務データ

| パラメータ | 型 | 説明 |
|---|---|---|
| `gov_enterprise_customer_count` | int | 官公庁・独法の法人契約顧客数 |
| `total_enterprise_customer_count` | int | 法人契約顧客総数 |
| `gov_monthly_revenue_jpy` | int | 官公庁向け月間売上高（円） |
| `total_monthly_revenue_jpy` | int | 法人事業月間売上高総額（円） |
| `private_network_service_count` | int | 閉域網・VPN接続サービス契約数 |
| `sdwan_service_customer_count` | int | SD-WANサービス契約顧客数 |
| `mobile_access_line_count` | int | 法人向けモバイルアクセス回線数 |
| `iot_sim_active_count` | int | アクティブIoT SIM数 |
| `cloud_connect_service_available` | bool | クラウド直接接続サービスの提供有無 |
| `ismap_partner_count` | int | ISMAP認定パートナーとの連携数 |
| `zero_trust_solution_available` | bool | ゼロトラストソリューションの提供有無 |
| `gov_sector_sales_headcount` | int | 官公庁向け営業担当者数 |
| `existing_gov_framework_contracts` | int | 既存の官公庁向け包括契約数 |

## 判断ロジック

### Step 1: 市場機会の規模推定

```
-- クラウド移行増加分に伴う通信需要の推定
cloud_gap_percent = target_cloud_ratio_percent - current_cloud_ratio_percent
incremental_procurement_value = annual_procurement_value_jpy × (cloud_gap_percent / 100)

-- クラウド移行案件における通信サービス比率（業界平均8-15%）
telecom_service_ratio = 0.12  -- 通信インフラが占める比率
addressable_market_jpy = incremental_procurement_value × telecom_service_ratio

-- 自社がアドレス可能な市場シェア推定
IF existing_gov_framework_contracts > 5 THEN
    estimated_share = 0.25      -- 既存取引実績豊富
ELSE IF existing_gov_framework_contracts > 0 THEN
    estimated_share = 0.15      -- 一定の実績あり
ELSE
    estimated_share = 0.05      -- 新規開拓が必要

revenue_opportunity_jpy = addressable_market_jpy × estimated_share
```

### Step 2: 需要変化パターンの予測

```
-- クラウド移行に伴い増加が見込まれるサービス
demand_increase_services = []

-- 閉域網/クラウド直接接続の需要
IF cloud_connect_service_available THEN
    demand_increase_services.append(("cloud_connect", "high"))
ELSE
    demand_increase_services.append(("cloud_connect", "high_but_unmet"))  -- 需要ありだが未提供

-- SD-WANの需要（マルチクラウド接続に伴う）
IF target_cloud_ratio_percent >= 70 THEN
    demand_increase_services.append(("sdwan", "high"))
ELSE
    demand_increase_services.append(("sdwan", "medium"))

-- モバイルアクセス（リモートワーク+クラウドアクセス）
demand_increase_services.append(("mobile_access", "medium"))

-- IoT回線（クラウド基盤上のIoTプラットフォーム需要）
demand_increase_services.append(("iot_sim", "medium"))

-- ゼロトラスト関連サービス
IF "zero_trust" IN security_requirements THEN
    IF zero_trust_solution_available THEN
        demand_increase_services.append(("zero_trust", "high"))
    ELSE
        demand_increase_services.append(("zero_trust", "high_but_unmet"))
```

### Step 3: 既存顧客への影響評価

```
-- 既存官公庁顧客のクラウド移行に伴う契約変更の可能性
gov_customer_ratio = gov_enterprise_customer_count / total_enterprise_customer_count

-- 既存の閉域網サービスの移行リスク（オンプレ前提の閉域網→クラウド接続へ）
IF private_network_service_count > 0 THEN
    migration_affected_count = private_network_service_count × (cloud_gap_percent / 100)
    IF migration_affected_count / private_network_service_count > 0.3 THEN
        existing_service_impact = "restructure"   -- 大幅なサービス再構成が必要
    ELSE IF migration_affected_count / private_network_service_count > 0.1 THEN
        existing_service_impact = "partial_change" -- 一部サービス変更
    ELSE
        existing_service_impact = "minimal"
ELSE
    existing_service_impact = "not_applicable"

-- 契約更改の機会としての評価
upsell_opportunity_count = gov_enterprise_customer_count × (cloud_gap_percent / 100) × 0.7
```

### Step 4: 自社対応準備度の評価

```
-- サービスポートフォリオの充足度
readiness_score = 0
readiness_max = 5

IF cloud_connect_service_available THEN readiness_score += 1
IF zero_trust_solution_available THEN readiness_score += 1
IF sdwan_service_customer_count > 10 THEN readiness_score += 1
IF ismap_partner_count >= 3 THEN readiness_score += 1
IF gov_sector_sales_headcount >= 5 THEN readiness_score += 1

readiness_ratio = readiness_score / readiness_max

IF readiness_ratio >= 0.8 THEN
    readiness = "well_prepared"
ELSE IF readiness_ratio >= 0.6 THEN
    readiness = "partially_prepared"
ELSE IF readiness_ratio >= 0.4 THEN
    readiness = "gaps_exist"
ELSE
    readiness = "significant_gaps"
```

### Step 5: 競争環境の評価

```
-- 競合他社の官公庁向け通信サービス態勢
-- （他キャリア・SIer・クラウド事業者との競合を考慮）
IF existing_gov_framework_contracts == 0 THEN
    competitive_position = "challenger"     -- 市場参入に課題
ELSE IF existing_gov_framework_contracts < 3 THEN
    competitive_position = "contender"      -- 一定の立場あり
ELSE
    competitive_position = "established"    -- 確固たるポジション

-- 適用開始までのリードタイム
months_to_enforcement = (target_fiscal_year - 2026) × 12 + 9  -- 4月開始想定
IF months_to_enforcement < 12 THEN
    time_pressure = "high"
ELSE IF months_to_enforcement < 24 THEN
    time_pressure = "medium"
ELSE
    time_pressure = "low"
```

### Step 6: 総合インパクトスコアの算出

```
-- ビジネス機会スコア（正のインパクト）
opportunity_score_raw = MIN(revenue_opportunity_jpy / 1000000000, 30)  -- 10億円=10pt, 上限30pt

-- 需要変化スコア
high_demand_count = COUNT(d FOR d IN demand_increase_services IF d[1] IN ("high", "high_but_unmet"))
demand_score = MIN(high_demand_count × 8, 25)

-- 既存契約への影響スコア
restructure_score = {"restructure": 15, "partial_change": 8, "minimal": 3, "not_applicable": 0}[existing_service_impact]

-- 準備度によるリスク加算
readiness_penalty = {"significant_gaps": 15, "gaps_exist": 10, "partially_prepared": 5, "well_prepared": 0}[readiness]

-- 時間的緊迫度
time_score = {"high": 15, "medium": 8, "low": 3}[time_pressure]

-- 総合スコア（機会 + 需要変化 + 既存影響 + 準備不足リスク + 時間圧力）
total_impact_score = opportunity_score_raw + demand_score + restructure_score + readiness_penalty + time_score
total_impact_score = MIN(total_impact_score, 100)

-- インパクトレベルの決定
IF total_impact_score >= 70 THEN impact_level = "critical"
ELSE IF total_impact_score >= 50 THEN impact_level = "high"
ELSE IF total_impact_score >= 30 THEN impact_level = "medium"
ELSE impact_level = "low"
```

## 出力

### インパクト評価結果

| 出力項目 | 型 | 説明 |
|---|---|---|
| `impact_level` | enum | `critical` / `high` / `medium` / `low` |
| `impact_score` | int | 総合インパクトスコア（0-100） |
| `confidence_score` | float | 分析確信度（0.00-1.00） |
| `impact_type` | enum | `opportunity` / `risk` / `mixed`（本スキルでは主に `opportunity`） |
| `revenue_opportunity_annual_jpy` | int | 年間新規収益機会（円） |
| `addressable_market_jpy` | int | アドレス可能市場規模（円） |
| `affected_gov_customer_count` | int | 影響を受ける官公庁顧客数 |
| `upsell_opportunity_count` | int | アップセル機会のある顧客数 |
| `service_gap_list` | list[string] | 不足しているサービス一覧 |
| `months_to_enforcement` | int | 施行開始までの月数 |
| `readiness` | enum | 対応準備度 |

### 推奨アクション

| 優先度 | アクション | 条件 |
|---|---|---|
| P0（即時） | クラウド直接接続サービスの開発・提供開始 | `cloud_connect_service_available` が `false` |
| P0（即時） | ゼロトラストネットワークサービスの商品化 | `zero_trust_solution_available` が `false` かつ `"zero_trust" IN security_requirements` |
| P1（1ヶ月以内） | 官公庁向けクラウド接続パッケージの企画・価格設計 | `readiness` が `partially_prepared` 以上 |
| P1（1ヶ月以内） | ISMAP認定クラウド事業者とのパートナーシップ強化（共同提案体制の構築） | `ismap_partner_count` が 3未満 |
| P2（3ヶ月以内） | 既存官公庁顧客への個別アプローチ計画の策定（クラウド移行ニーズのヒアリング） | `existing_gov_framework_contracts` が 1以上 |
| P2（3ヶ月以内） | 官公庁向け営業体制の強化（専任チーム増員・教育） | `gov_sector_sales_headcount` が 5未満 |
| P2（3ヶ月以内） | 閉域網サービスのクラウド接続対応への刷新計画策定 | `existing_service_impact` が `restructure` |
| P3（6ヶ月以内） | SD-WAN＋セキュリティ統合サービス（SASE）のラインナップ整備 | `sdwan_service_customer_count` が 10未満 |
| P3（6ヶ月以内） | 法人IoT回線のクラウドプラットフォーム連携機能の強化 | `iot_sim_active_count` が 1000以上 |

### 通知テンプレート

```
【{priority}】官公庁「クラウド・バイ・デフォルト2.0」に伴う法人通信需要の変化

■ インパクトレベル: {impact_level}（スコア: {impact_score}/100）
■ インパクト種別: ビジネス機会
■ 年間収益機会: {revenue_opportunity_annual_jpy:,}円
■ 対象市場規模: {addressable_market_jpy:,}円
■ 施行開始まで: {months_to_enforcement}ヶ月

経済産業省が新規IT調達の{target_cloud_ratio_percent}%以上を
クラウド前提とする「クラウド・バイ・デフォルト2.0」を発表しました。
年間{annual_procurement_count:,}件・約{annual_procurement_value_jpy:,}円の
調達案件が対象となります。

■ 当社への影響分析:
- 官公庁顧客数: {gov_enterprise_customer_count}社
- アップセル対象: {upsell_opportunity_count}社
- 対応準備度: {readiness}
- 不足サービス: {service_gap_list}

■ 必須対応事項:
- ゼロトラストアーキテクチャ対応: {zero_trust_status}
- クラウド直接接続: {cloud_connect_status}
- ISMAP連携パートナー: {ismap_partner_count}社

【推奨アクション】
{recommended_actions}

法人営業部門と連携し、提案機会を最大化してください。
```

## 参照データソース

### Fabric テーブル（sqldb_mobile_01）

| テーブル名 | 用途 |
|---|---|
| `customers` | 法人顧客（`customer_type = 'corporate'`）の抽出、官公庁顧客の特定 |
| `contracts` | 法人向けプラン（ビジネスプラン・データSIM）の契約状況、閉域網オプション契約の集計 |
| `transactions` | 官公庁向け月間売上の集計、法人サービス別の収益分析 |
| `inventory` | 法人向けルーター・IoTデバイスの在庫状況確認 |

### Fabric テーブル（sqldb_common_01）

| テーブル名 | 用途 |
|---|---|
| `unified_customers` | 官公庁顧客の統合顧客ベースでの特定（`industry_code` フィルタ） |
| `customer_segments` | 法人セグメント（premium法人等）の抽出、LTVに基づく優先顧客特定 |
| `domain_id_mappings` | SI事業との共通顧客特定（SI案件と連動した通信需要の把握） |

### Fabric テーブル（sqldb_si_01）

| テーブル名 | 用途 |
|---|---|
| `customers` | SI事業側の官公庁顧客リストとの突合（共同提案候補の特定） |
| `contracts` | クラウド移行プロジェクトの進行状況確認（関連通信需要の先行把握） |
| `resource_inventory` | クラウド・ゼロトラスト関連エンジニアの稼働状況（技術支援リソースの確認） |

### Fabric テーブル（sqldb_news_01）

| テーブル名 | 用途 |
|---|---|
| `news_articles` | クラウド政策関連ニュースの時系列追跡 |
| `impact_analyses` | SI事業側の分析結果参照（クロスドメイン連携の判断材料） |
| `notifications` | 過去の関連通知の確認、SI事業部への同時通知の調整 |

### 外部参照データ

| データソース | 用途 |
|---|---|
| ISMAP クラウドサービスリスト | 政府認定クラウドの最新リスト・パートナー候補の特定 |
| 経済産業省 IT調達公告 | 具体的な調達案件のモニタリング |
| 総務省 自治体DX推進計画 | 地方自治体への波及を含む将来需要の推定 |
