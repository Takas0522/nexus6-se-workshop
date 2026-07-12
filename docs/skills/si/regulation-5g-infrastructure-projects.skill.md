# SI事業：5G帯域拡張に伴う通信インフラ構築プロジェクト機会インパクト判断スキル

## 概要

5G周波数帯の追加割当（3.8GHz帯120MHz幅）に伴い、通信キャリア各社が実施する基地局増設・ネットワーク最適化・干渉対策等の設備投資がSI事業にもたらすプロジェクト受注機会を判断するスキル。業界全体で約4,500億円と試算される設備投資のうち、SI/SIer領域が獲得可能なシステムインテグレーション案件（基地局制御ソフトウェア、ネットワーク管理システム、周波数管理・干渉検知システム、5G SA/NSAコア網構築等）の規模と自社の受注可能性を評価する。

## 入力パラメータ

### ニュース情報

| パラメータ | 型 | 説明 |
|---|---|---|
| `news_category` | string | ニュースカテゴリ（`regulation`） |
| `frequency_band_mhz` | string | 割当対象周波数帯（例: `3800-3920`） |
| `bandwidth_mhz` | int | 割当帯域幅（MHz） |
| `traffic_growth_rate_percent` | float | 5Gトラフィック増加率（%） |
| `estimated_industry_capex_jpy` | int | 業界全体の設備投資試算額（円） |
| `carrier_count` | int | 割当審査対象キャリア数 |
| `base_station_vendors` | list[string] | 主要基地局メーカー（例: `["NEC", "Nokia Japan"]`） |
| `satellite_interference_risk` | bool | 衛星通信との干渉リスクの有無 |
| `allocation_review_start_year` | int | 割当審査開始年 |

### 業務データ

| パラメータ | 型 | 説明 |
|---|---|---|
| `total_active_projects` | int | 現在進行中のプロジェクト数 |
| `telecom_sector_projects` | int | 通信業界向けプロジェクト数 |
| `telecom_sector_revenue_ratio_percent` | float | 通信業界売上比率（%） |
| `telecom_clients` | list[string] | 通信業界の既存顧客一覧 |
| `carrier_client_count` | int | 取引のあるキャリア数 |
| `5g_project_experience_count` | int | 5G関連プロジェクトの実績件数 |
| `network_engineering_staff` | int | ネットワークエンジニア数 |
| `total_engineering_staff` | int | エンジニア総数 |
| `available_engineers` | int | 現在アサイン可能なエンジニア数 |
| `avg_project_monthly_revenue_jpy` | int | プロジェクト平均月間収益（円） |
| `partner_network_count` | int | 協力会社・パートナー数 |
| `certifications_5g_network` | int | 5G/ネットワーク関連資格保有者数 |
| `annual_revenue_jpy` | int | SI事業年間売上（円） |
| `current_utilization_rate_percent` | float | 現在の稼働率（%） |
| `satellite_system_experience` | bool | 衛星通信システムの開発経験有無 |
| `spectrum_management_experience` | bool | 周波数管理システムの開発経験有無 |

## 判断ロジック

### Step 1: 市場機会の規模推定

```
-- 設備投資額のうちSI/ソフトウェア領域の比率推定
-- 基地局ハードウェア60%、土木工事15%、SI/ソフトウェア25%が業界平均
si_addressable_market_jpy = estimated_industry_capex_jpy × 0.25

-- SI対象領域の内訳
-- ネットワーク管理システム: 35%
-- 基地局制御ソフトウェア: 25%
-- コア網構築・移行: 20%
-- 周波数管理・干渉対策: 10%
-- テスト・検証: 10%
network_mgmt_market = si_addressable_market_jpy × 0.35
base_station_sw_market = si_addressable_market_jpy × 0.25
core_network_market = si_addressable_market_jpy × 0.20
spectrum_mgmt_market = si_addressable_market_jpy × 0.10
testing_market = si_addressable_market_jpy × 0.10

-- 自社が参入可能な市場の推定
-- キャリアとの取引実績に基づくシェア推定
IF carrier_client_count >= 3 THEN
    market_access = "broad"               -- 主要キャリアの大半と取引あり
    addressable_share_percent = 8         -- 市場の8%獲得可能性
ELSE IF carrier_client_count >= 2 THEN
    market_access = "moderate"
    addressable_share_percent = 5
ELSE IF carrier_client_count >= 1 THEN
    market_access = "limited"
    addressable_share_percent = 2
ELSE
    market_access = "none"
    addressable_share_percent = 0.5       -- 新規参入として

potential_revenue_jpy = si_addressable_market_jpy × (addressable_share_percent / 100)
```

### Step 2: 技術力・実績による競争力評価

```
-- 5G関連プロジェクト実績
IF 5g_project_experience_count >= 10 THEN
    experience_level = "expert"
    win_probability_base = 0.35           -- 提案時の受注確率ベース
ELSE IF 5g_project_experience_count >= 5 THEN
    experience_level = "experienced"
    win_probability_base = 0.25
ELSE IF 5g_project_experience_count >= 2 THEN
    experience_level = "developing"
    win_probability_base = 0.15
ELSE
    experience_level = "novice"
    win_probability_base = 0.08

-- ネットワークエンジニアの充足度
network_engineer_ratio = network_engineering_staff / total_engineering_staff
IF network_engineer_ratio >= 0.2 THEN
    network_capability = "strong"
    win_probability_modifier = 1.3
ELSE IF network_engineer_ratio >= 0.1 THEN
    network_capability = "adequate"
    win_probability_modifier = 1.1
ELSE IF network_engineer_ratio >= 0.05 THEN
    network_capability = "limited"
    win_probability_modifier = 0.9
ELSE
    network_capability = "insufficient"
    win_probability_modifier = 0.6

-- 周波数管理・衛星干渉の専門性（差別化要因）
IF spectrum_management_experience AND satellite_system_experience THEN
    specialization_bonus = 0.15           -- 干渉対策が焦点の市場で優位
ELSE IF spectrum_management_experience OR satellite_system_experience THEN
    specialization_bonus = 0.08
ELSE
    specialization_bonus = 0.0

-- 最終受注確率
win_probability = MIN(win_probability_base × win_probability_modifier + specialization_bonus, 0.5)

-- 期待受注額
expected_revenue_jpy = potential_revenue_jpy × win_probability
```

### Step 3: リソースキャパシティの評価

```
-- 案件を受注した場合の要員確保可能性
-- 想定プロジェクト規模（平均10名×12ヶ月）
estimated_project_size_persons = 10
estimated_project_duration_months = 12

-- 現在の余剰リソース
IF available_engineers >= estimated_project_size_persons × 2 THEN
    resource_readiness = "ready"                  -- 複数案件に即対応可
    staffing_risk = "low"
ELSE IF available_engineers >= estimated_project_size_persons THEN
    resource_readiness = "single_project"         -- 1案件なら対応可
    staffing_risk = "moderate"
ELSE IF available_engineers >= estimated_project_size_persons × 0.5 THEN
    resource_readiness = "partial"                -- パートナー活用が必須
    staffing_risk = "high"
ELSE
    resource_readiness = "constrained"            -- 大幅な調達が必要
    staffing_risk = "critical"

-- 稼働率から見た受け入れ余力
IF current_utilization_rate_percent >= 90 THEN
    utilization_pressure = "critical"             -- ほぼ満稼働
    max_new_projects = 1
ELSE IF current_utilization_rate_percent >= 80 THEN
    utilization_pressure = "high"
    max_new_projects = 2
ELSE IF current_utilization_rate_percent >= 70 THEN
    utilization_pressure = "moderate"
    max_new_projects = 4
ELSE
    utilization_pressure = "comfortable"
    max_new_projects = 6

-- パートナー活用による補完
IF partner_network_count >= 20 THEN
    partner_leverage = "strong"                   -- 大規模案件もパートナー込みで対応
ELSE IF partner_network_count >= 10 THEN
    partner_leverage = "moderate"
ELSE
    partner_leverage = "limited"
```

### Step 4: 案件タイムラインの予測

```
-- 割当審査開始後のプロジェクト発生タイミング
-- 審査開始 → 割当決定（6ヶ月）→ キャリア設備計画策定（3ヶ月）→ RFP発行（3ヶ月）
-- = 審査開始から約12ヶ月後に案件化

months_to_rfp = 12
months_to_project_start = months_to_rfp + 3    -- RFP→選定→契約

-- ピーク時期の予測
-- 割当後2-3年が設備投資のピーク
peak_start_months = 18
peak_end_months = 36
peak_duration_months = peak_end_months - peak_start_months

-- 準備期間の猶予
IF months_to_rfp <= 6 THEN
    preparation_urgency = "immediate"
ELSE IF months_to_rfp <= 12 THEN
    preparation_urgency = "near_term"
ELSE
    preparation_urgency = "planned"

-- 案件数の時系列予測
-- Year 1: 全体の20%（初期案件）
-- Year 2: 全体の45%（ピーク）
-- Year 3: 全体の35%（継続・運用移行）
year1_projects = MAX(ROUND(potential_revenue_jpy / avg_project_monthly_revenue_jpy / 12 × 0.20), 1)
year2_projects = MAX(ROUND(potential_revenue_jpy / avg_project_monthly_revenue_jpy / 12 × 0.45), 1)
year3_projects = MAX(ROUND(potential_revenue_jpy / avg_project_monthly_revenue_jpy / 12 × 0.35), 1)
```

### Step 5: リスク要因の評価

```
-- 衛星通信干渉問題による案件遅延リスク
IF satellite_interference_risk THEN
    -- 技術的共用条件の合意に時間がかかる可能性
    delay_risk = "medium"
    potential_delay_months = 6
    -- ただし干渉対策自体が新たな案件になる可能性も
    IF satellite_system_experience THEN
        interference_opportunity = "strong"       -- 干渉対策案件の受注チャンス
    ELSE
        interference_opportunity = "limited"
ELSE
    delay_risk = "low"
    potential_delay_months = 0
    interference_opportunity = "none"

-- 競合リスク（大手SIerとの競争）
IF experience_level IN ("expert", "experienced") AND network_capability IN ("strong", "adequate") THEN
    competitive_risk = "manageable"
ELSE IF experience_level IN ("developing") THEN
    competitive_risk = "high"                     -- 実績不足で不利
ELSE
    competitive_risk = "severe"                   -- 受注は困難

-- 技術陳腐化リスク（O-RAN等の新アーキテクチャへの対応）
IF certifications_5g_network >= 10 THEN
    tech_currency_risk = "low"
ELSE IF certifications_5g_network >= 5 THEN
    tech_currency_risk = "moderate"
ELSE
    tech_currency_risk = "high"
```

### Step 6: 総合インパクトスコアの算出

```
-- 機会スコア
market_size_score = CASE
    WHEN expected_revenue_jpy > annual_revenue_jpy × 0.1 THEN 25   -- 年商10%超のインパクト
    WHEN expected_revenue_jpy > annual_revenue_jpy × 0.05 THEN 20
    WHEN expected_revenue_jpy > annual_revenue_jpy × 0.02 THEN 14
    ELSE 7
END

capability_score = CASE
    WHEN win_probability >= 0.30 THEN 20
    WHEN win_probability >= 0.20 THEN 15
    WHEN win_probability >= 0.12 THEN 10
    ELSE 5
END

resource_score = CASE
    WHEN resource_readiness == "ready" THEN 15
    WHEN resource_readiness == "single_project" THEN 12
    WHEN resource_readiness == "partial" THEN 8
    ELSE 4
END

-- リスク/制約スコア
timing_score = CASE
    WHEN preparation_urgency == "immediate" THEN 15
    WHEN preparation_urgency == "near_term" THEN 10
    ELSE 5
END

risk_deduction = CASE
    WHEN competitive_risk == "severe" THEN -10
    WHEN competitive_risk == "high" THEN -5
    ELSE 0
END

-- 干渉対策案件のボーナス
interference_bonus = CASE
    WHEN interference_opportunity == "strong" THEN 10
    WHEN interference_opportunity == "limited" THEN 3
    ELSE 0
END

-- 総合スコア
total_impact_score = market_size_score + capability_score + resource_score + timing_score + risk_deduction + interference_bonus
total_impact_score = MAX(MIN(total_impact_score, 100), 0)

-- インパクトレベルの決定
IF total_impact_score >= 70 THEN impact_level = "critical"
ELSE IF total_impact_score >= 55 THEN impact_level = "high"
ELSE IF total_impact_score >= 35 THEN impact_level = "medium"
ELSE IF total_impact_score >= 20 THEN impact_level = "low"
ELSE impact_level = "minimal"

-- SI事業にとっては主に機会型のインパクト
impact_type = "opportunity"
```

## 出力

### インパクト評価結果

| 出力項目 | 型 | 説明 |
|---|---|---|
| `impact_level` | enum | `critical` / `high` / `medium` / `low` / `minimal` |
| `impact_score` | int | 総合インパクトスコア（0-100） |
| `confidence_score` | float | 分析確信度（0.00-1.00） |
| `impact_type` | enum | `opportunity`（機会型） |
| `si_addressable_market_jpy` | int | SI領域の参入可能市場規模（円） |
| `expected_revenue_jpy` | int | 期待受注額（円） |
| `win_probability` | float | 受注確率 |
| `resource_readiness` | string | リソース準備状況 |
| `preparation_urgency` | string | 準備の緊急度 |
| `year1_projects` | int | 1年目の想定案件数 |
| `year2_projects` | int | 2年目の想定案件数（ピーク） |
| `year3_projects` | int | 3年目の想定案件数 |
| `competitive_risk` | string | 競合リスク |
| `interference_opportunity` | string | 干渉対策案件の機会 |
| `peak_revenue_period` | string | 収益ピーク期間 |

### 推奨アクション

| 優先度 | アクション | 条件 |
|---|---|---|
| P0（即時） | 既存キャリア顧客への5G帯域拡張対応ヒアリング・早期情報収集 | `carrier_client_count` が 1 以上 |
| P1（1ヶ月以内） | 5G/ネットワークエンジニアの採用・育成計画策定 | `network_capability` が `limited` または `insufficient` |
| P1（1ヶ月以内） | 基地局メーカー（NEC・ノキア・ジャパン）との協業体制の構築・強化 | `market_access` が `moderate` 以上 |
| P1（1ヶ月以内） | 衛星通信干渉対策の技術調査・ソリューション企画 | `satellite_interference_risk` が `true` AND `satellite_system_experience` が `true` |
| P2（3ヶ月以内） | 周波数管理システム・電波干渉検知ソリューションの提案書作成 | `spectrum_management_experience` が `true` |
| P2（3ヶ月以内） | 5G SA/NSAコア網構築の検証環境整備・PoC準備 | `experience_level` が `developing` 以上 |
| P2（3ヶ月以内） | パートナー企業との5G案件向けアライアンス強化 | `staffing_risk` が `high` または `critical` |
| P3（6ヶ月以内） | O-RAN対応の技術力強化（研修・資格取得） | `tech_currency_risk` が `moderate` 以上 |
| P3（6ヶ月以内） | RFP対応チームの事前組成と提案テンプレート整備 | `preparation_urgency` が `near_term` |
| P3（6ヶ月以内） | 5Gネットワーク構築実績のケーススタディ・実績資料の整備 | 常時実行 |

### 通知テンプレート

```
【{priority}】5G帯域拡張に伴うSIプロジェクト機会の分析

■ インパクトレベル: {impact_level}（スコア: {impact_score}/100）
■ インパクト種別: {impact_type}
■ SI参入可能市場: {si_addressable_market_jpy:,}円
■ 期待受注額: {expected_revenue_jpy:,}円（受注確率: {win_probability:.0%}）
■ ピーク期間: {peak_revenue_period}

総務省による3.8GHz帯（{bandwidth_mhz}MHz幅）の追加割当決定に伴い、
業界全体で約{estimated_industry_capex_jpy:,}円の設備投資が見込まれます。

【案件機会】
- SI対象市場規模: {si_addressable_market_jpy:,}円
- 1年目想定案件数: {year1_projects}件
- 2年目想定案件数: {year2_projects}件（ピーク）
- 3年目想定案件数: {year3_projects}件
- 干渉対策案件: {interference_opportunity}

【自社競争力】
- 5G実績レベル: {experience_level}
- ネットワーク技術力: {network_capability}
- リソース準備状況: {resource_readiness}
- 競合リスク: {competitive_risk}

【対応スケジュール】
- RFP発行見込み: 約{months_to_rfp}ヶ月後
- 準備緊急度: {preparation_urgency}

【推奨アクション】
{recommended_actions}

通信インフラ投資の波は2-3年継続が見込まれます。
早期の体制構築により、ピーク期での最大受注を目指してください。
```

## 参照データソース

### Fabric テーブル（sqldb_si_01）

| テーブル名 | 用途 |
|---|---|
| `customers` | 通信キャリア・基地局メーカーとの取引実績、業界コード別の顧客分析 |
| `contracts` | 5G関連・ネットワーク構築プロジェクトの過去実績、契約規模の集計 |
| `transactions` | 通信業界向け売上の推移分析、案件収益性の評価 |
| `resource_inventory` | ネットワークエンジニアの在籍数・スキル分布・稼働状況 |

### Fabric テーブル（sqldb_mobile_01）

| テーブル名 | 用途 |
|---|---|
| `contracts` | 自社携帯電話事業の5G関連契約動向（グループシナジーの把握） |
| `inventory` | 5G対応端末・基地局関連の在庫状況から市場トレンドを把握 |

### Fabric テーブル（sqldb_common_01）

| テーブル名 | 用途 |
|---|---|
| `unified_customers` | 通信業界顧客の統合的な取引状況（SI+モバイル事業の横断分析） |
| `domain_id_mappings` | キャリア顧客が複数事業領域で取引しているケースの特定 |
| `customer_segments` | 通信業界顧客のLTV・リスクスコア分析 |

### Fabric テーブル（sqldb_news_01）

| テーブル名 | 用途 |
|---|---|
| `news_articles` | 5G・通信インフラ投資関連ニュースの継続モニタリング |
| `impact_analyses` | 携帯電話事業での同一ニュース分析結果（キャリア側の投資計画への示唆） |
| `notifications` | 過去の通信案件関連通知との整合・追加情報の統合 |

### 外部参照データ

| データソース | 用途 |
|---|---|
| 総務省 電波利用ホームページ | 周波数割当の詳細条件・スケジュールの公式情報 |
| MIC 情報通信統計データベース | 通信設備投資の時系列データ・キャリア別投資額 |
| 各キャリアIR資料 | 設備投資計画・5G展開ロードマップ |
| O-RAN Alliance仕様書 | Open RAN対応の技術要件 |
