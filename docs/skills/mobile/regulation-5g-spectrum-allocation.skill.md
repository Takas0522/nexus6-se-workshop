# 携帯電話事業：5G周波数帯追加割当インパクト判断スキル

## 概要

総務省による5G周波数帯の追加割当（3.8GHz帯：3800〜3920MHz、120MHz幅）が携帯電話事業に与えるインパクトを判断するスキル。新帯域取得に伴う設備投資計画の見直し、対応端末の調達戦略、衛星通信との周波数共用リスク、および競合他社の動向を評価し、事業機会と財務影響の両面から推奨アクションを出力する。

## 入力パラメータ

### ニュース情報

| パラメータ | 型 | 説明 |
|---|---|---|
| `news_category` | string | ニュースカテゴリ（`regulation`） |
| `regulation_authority` | string | 規制当局名（例: `総務省`） |
| `frequency_band_mhz` | string | 割当対象周波数帯（例: `3800-3920`） |
| `bandwidth_mhz` | int | 割当帯域幅（MHz）（例: `120`） |
| `generation` | enum | `5G` / `5G-Advanced` / `6G` |
| `allocation_timeline_months` | int | 割当審査開始までの期間（月） |
| `estimated_industry_capex_jpy` | int | 業界全体の設備投資試算額（円） |
| `eligible_carriers_count` | int | 審査対象事業者数 |
| `traffic_growth_rate_percent` | float | 直近のトラフィック増加率（%） |
| `interference_risk_level` | enum | `high` / `medium` / `low` / `none` |

### 業務データ

| パラメータ | 型 | 説明 |
|---|---|---|
| `current_5g_bandwidth_mhz` | int | 自社保有の5G帯域幅合計（MHz） |
| `base_station_count` | int | 自社5G基地局数 |
| `annual_capex_budget_jpy` | int | 年間設備投資予算（円） |
| `5g_coverage_population_percent` | float | 5G人口カバー率（%） |
| `5g_subscriber_count` | int | 5G契約者数 |
| `total_subscriber_count` | int | 総契約者数 |
| `peak_utilization_rate_percent` | float | ピーク時帯域使用率（%） |
| `compatible_device_ratio_percent` | float | 3.8GHz帯対応端末の流通比率（%） |
| `inventory_handset_count` | int | 端末在庫数 |
| `vendor_contracts` | list[string] | 基地局ベンダー契約先（例: `NEC`, `Nokia`, `Ericsson`） |

## 判断ロジック

### Step 1: 帯域逼迫度の評価

```
-- 現在の帯域使用率に基づく緊急度判定
IF peak_utilization_rate_percent >= 85 THEN
    urgency = "critical"        -- 即座に追加帯域が必要
ELSE IF peak_utilization_rate_percent >= 70 THEN
    urgency = "high"            -- 1年以内に逼迫が予想される
ELSE IF peak_utilization_rate_percent >= 50 THEN
    urgency = "moderate"        -- 中期的な計画で対応可能
ELSE
    urgency = "low"             -- 当面余裕あり

-- トラフィック増加率を考慮した将来予測
months_to_saturation = (100 - peak_utilization_rate_percent) / (traffic_growth_rate_percent / 12)
IF months_to_saturation < 12 THEN urgency = MAX(urgency, "critical")
ELSE IF months_to_saturation < 24 THEN urgency = MAX(urgency, "high")
```

### Step 2: 設備投資影響の算出

```
-- 自社に必要な追加投資額の推定
-- 業界全体の投資額を事業者数で按分し、帯域シェアで補正
estimated_capex_per_carrier = estimated_industry_capex_jpy / eligible_carriers_count
bandwidth_expansion_ratio = bandwidth_mhz / current_5g_bandwidth_mhz
capex_requirement_jpy = estimated_capex_per_carrier × bandwidth_expansion_ratio

-- 予算充足度の評価
capex_budget_ratio = capex_requirement_jpy / annual_capex_budget_jpy
IF capex_budget_ratio > 0.5 THEN
    financial_pressure = "severe"   -- 予算の50%超を占める
ELSE IF capex_budget_ratio > 0.3 THEN
    financial_pressure = "high"     -- 予算の30〜50%
ELSE IF capex_budget_ratio > 0.15 THEN
    financial_pressure = "moderate" -- 予算の15〜30%
ELSE
    financial_pressure = "manageable" -- 予算内で吸収可能
```

### Step 3: 端末対応状況の評価

```
-- 3.8GHz帯対応端末の在庫・流通状況
IF compatible_device_ratio_percent >= 70 THEN
    device_readiness = "ready"          -- 市場に十分な対応端末あり
ELSE IF compatible_device_ratio_percent >= 40 THEN
    device_readiness = "partial"        -- 一部モデルのみ対応
ELSE IF compatible_device_ratio_percent >= 10 THEN
    device_readiness = "limited"        -- 少数のハイエンドモデルのみ
ELSE
    device_readiness = "not_available"  -- 未対応、開発・調達が必要

-- 在庫への影響評価
IF device_readiness IN ("limited", "not_available") THEN
    inventory_impact = "high"   -- 既存在庫が新帯域非対応、買い替え促進必要
ELSE
    inventory_impact = "low"
```

### Step 4: 競争環境の影響評価

```
-- 新規参入リスクの評価
IF eligible_carriers_count > 4 THEN
    new_entrant_risk = "high"       -- 既存4社以外の新規参入あり
ELSE
    new_entrant_risk = "low"        -- 既存事業者間の配分

-- 帯域取得競争の激しさ
competition_intensity = eligible_carriers_count / (bandwidth_mhz / 20)  -- 20MHz単位での割当想定
IF competition_intensity > 2.0 THEN
    allocation_risk = "high"    -- 希望帯域を取得できないリスク大
ELSE IF competition_intensity > 1.0 THEN
    allocation_risk = "medium"
ELSE
    allocation_risk = "low"     -- 全事業者に十分な帯域あり
```

### Step 5: 衛星通信干渉リスクの評価

```
-- 技術的共用条件による制約
IF interference_risk_level == "high" THEN
    deployment_constraint = "significant"  -- 基地局配置に大きな制約
    coverage_delay_months = 6              -- カバレッジ展開に6ヶ月遅延
ELSE IF interference_risk_level == "medium" THEN
    deployment_constraint = "moderate"     -- 一部地域で制約あり
    coverage_delay_months = 3
ELSE
    deployment_constraint = "minimal"
    coverage_delay_months = 0
```

### Step 6: 総合インパクトスコアの算出

```
-- スコアリング（0-100）
urgency_score = {"critical": 30, "high": 22, "moderate": 15, "low": 5}[urgency]
financial_score = {"severe": 25, "high": 20, "moderate": 12, "manageable": 5}[financial_pressure]
device_score = {"not_available": 20, "limited": 15, "partial": 8, "ready": 3}[device_readiness]
competition_score = {"high": 15, "medium": 10, "low": 5}[allocation_risk]
interference_score = {"significant": 10, "moderate": 6, "minimal": 2}[deployment_constraint]

total_impact_score = urgency_score + financial_score + device_score + competition_score + interference_score

-- インパクトレベルの決定
IF total_impact_score >= 75 THEN impact_level = "critical"
ELSE IF total_impact_score >= 55 THEN impact_level = "high"
ELSE IF total_impact_score >= 35 THEN impact_level = "medium"
ELSE impact_level = "low"
```

## 出力

### インパクト評価結果

| 出力項目 | 型 | 説明 |
|---|---|---|
| `impact_level` | enum | `critical` / `high` / `medium` / `low` |
| `impact_score` | int | 総合インパクトスコア（0-100） |
| `confidence_score` | float | 分析確信度（0.00-1.00） |
| `affected_subscriber_count` | int | 影響を受ける契約者数（5G契約者全体） |
| `estimated_capex_requirement_jpy` | int | 自社に必要な追加設備投資額（円） |
| `estimated_revenue_opportunity_jpy` | int | 帯域拡張による新規収益機会（円/年） |
| `months_to_saturation` | int | 帯域飽和までの予測月数 |
| `coverage_delay_months` | int | 干渉制約によるカバレッジ展開遅延月数 |

### 推奨アクション

| 優先度 | アクション | 条件 |
|---|---|---|
| P0（即時） | 割当審査への申請準備・周波数利用計画の策定 | `urgency` が `critical` または `high` |
| P0（即時） | 設備投資計画の見直し・資金計画の策定 | `financial_pressure` が `severe` または `high` |
| P1（1ヶ月以内） | 基地局ベンダーとの3.8GHz帯対応協議開始 | 常時実行 |
| P1（1ヶ月以内） | 対応端末ラインナップの調達計画策定 | `device_readiness` が `limited` 以下 |
| P2（3ヶ月以内） | 衛星通信事業者との技術的共用条件の協議参加 | `interference_risk_level` が `medium` 以上 |
| P2（3ヶ月以内） | 既存5G契約者への新帯域対応端末の買い替え促進施策の設計 | `inventory_impact` が `high` |
| P3（6ヶ月以内） | 新帯域活用サービス（超低遅延、ローカル5G連携等）の企画 | `urgency` が `moderate` 以下 |
| P3（6ヶ月以内） | 法人顧客向け新帯域活用ソリューションの提案資料作成 | 常時実行 |

### 通知テンプレート

```
【{priority}】5G周波数帯追加割当（3.8GHz帯）に関する影響分析レポート

■ インパクトレベル: {impact_level}（スコア: {impact_score}/100）
■ 影響契約者数: {affected_subscriber_count:,}名
■ 追加設備投資見込み: {estimated_capex_requirement_jpy:,}円
■ 帯域飽和予測: {months_to_saturation}ヶ月後

総務省が3.8GHz帯（120MHz幅）の追加開放を正式発表しました。
ピーク時帯域使用率が{peak_utilization_rate_percent}%に達しており、
{urgency_description}。

【推奨アクション】
{recommended_actions}

詳細は添付の分析レポートをご確認ください。
```

## 参照データソース

### Fabric テーブル（sqldb_mobile_01）

| テーブル名 | 用途 |
|---|---|
| `customers` | 5G契約者数の集計、顧客セグメント別影響分析 |
| `contracts` | 5G対応プラン契約数、プラン別帯域利用特性の把握 |
| `transactions` | 直近6ヶ月の通信量トレンド、データ超過料金発生状況の分析 |
| `inventory` | 3.8GHz帯対応端末の在庫状況、非対応端末の残存比率確認 |

### Fabric テーブル（sqldb_common_01）

| テーブル名 | 用途 |
|---|---|
| `unified_customers` | 統合顧客ベースでの影響範囲特定 |
| `customer_segments` | セグメント別（premium/standard/entry）の優先対応判断 |
| `domain_id_mappings` | 携帯電話事業固有IDとの紐付け |

### Fabric テーブル（sqldb_news_01）

| テーブル名 | 用途 |
|---|---|
| `news_articles` | 同カテゴリの関連記事取得、時系列トレンド把握 |
| `impact_analyses` | 過去の類似規制ニュースに対する分析結果の参照 |
| `notifications` | 過去の通知履歴と既読状況の確認 |

### 外部参照データ

| データソース | 用途 |
|---|---|
| 総務省 電波利用ホームページ | 割当審査スケジュール、技術条件の最新情報 |
| 情報通信審議会 議事録 | 有識者会議の検討状況、共用条件の方向性 |
| 端末メーカー製品ロードマップ | 3.8GHz帯対応端末の発売スケジュール |
