# スキル: AI推論基盤テクノロジーインパクト分析

## 概要

GPU・半導体等のAI推論基盤に関する技術革新ニュースを分析し、当社AIエージェント事業の推論インフラコスト、性能計画、調達戦略への影響を判断するスキル。新GPU世代の性能向上率・電力効率・価格・供給時期から、現行インフラとの比較優位性を算出し、インフラ更改・クラウド調達計画の見直し要否を決定する。

## 入力パラメータ

### ニュース情報

| パラメータ | 型 | 説明 |
|-----------|-----|------|
| article_id | string | ニュース記事ID |
| title | string | 記事タイトル |
| summary | string | 記事要約 |
| category | string | カテゴリ（technology） |
| published_at | datetime | 配信日時 |
| extracted_entities | object[] | 抽出エンティティ（GPU名、性能倍率、価格、出荷時期、プロセスノード等） |

### 業務データ

| パラメータ | 型 | 説明 | ソース |
|-----------|-----|------|--------|
| current_gpu_generation | string | 現行利用GPUの世代 | インフラ管理台帳 |
| current_gpu_count | int | 現行GPU保有・利用数 | sqldb_ai_agent_01.products + インフラ台帳 |
| monthly_inference_cost | decimal | 月間推論インフラコスト（円） | sqldb_ai_agent_01.transactions (type='infra_cost') |
| monthly_inference_volume | bigint | 月間推論リクエスト数 | sqldb_ai_agent_01.transactions (type='api_call') |
| cloud_provider_contracts | object[] | クラウドプロバイダ契約情報（期間、コミット額） | sqldb_ai_agent_01.contracts |
| infra_refresh_plan_date | date | 次期インフラ更改計画日 | 社内計画データ |
| power_consumption_monthly_kwh | decimal | 月間消費電力量（kWh） | インフラ台帳 |
| cost_per_inference_request | decimal | 推論リクエストあたりコスト（円） | 算出値 |

## 判断ロジック

### Step 1: 技術世代ギャップ評価

```
new_gpu_generation = extracted_entities.gpu_name  -- "Blackwell Ultra B300"
performance_gain = extracted_entities.performance_multiplier  -- 2.5
efficiency_gain = extracted_entities.efficiency_multiplier  -- 1.8

generation_gap = calculate_generation_gap(current_gpu_generation, new_gpu_generation)
-- 例: B200 → B300 = 1世代差, A100 → B300 = 3世代差

IF generation_gap >= 3 THEN
    tech_obsolescence_risk = 'high'
ELIF generation_gap >= 2 THEN
    tech_obsolescence_risk = 'medium'
ELSE
    tech_obsolescence_risk = 'low'
END IF
```

### Step 2: コスト削減ポテンシャル算出

```
-- 新GPUへの移行による月間コスト削減見込み
projected_cost_reduction_ratio = 1 - (1 / efficiency_gain)
-- B300: 1 - (1/1.8) = 0.444 → 44.4%削減

projected_monthly_saving = monthly_inference_cost * projected_cost_reduction_ratio

-- 年間コスト削減ポテンシャル
annual_saving_potential = projected_monthly_saving * 12

-- 移行コスト概算（GPU単価 × 必要数 / 性能向上分）
new_gpu_price = extracted_entities.price_per_node  -- $650,000
required_nodes = CEIL(current_gpu_count / (performance_gain * 8))  -- 8GPU/node
migration_cost = new_gpu_price * required_nodes * exchange_rate_usd_jpy

-- ROI（投資回収期間）
roi_months = migration_cost / projected_monthly_saving
```

### Step 3: インパクトスコア算出

| 条件 | スコア範囲 | レベル |
|------|-----------|--------|
| annual_saving_potential >= 5億円 AND roi_months <= 12 | 75.0–100.0 | high |
| annual_saving_potential >= 2億円 AND roi_months <= 18 | 50.0–74.9 | medium |
| annual_saving_potential >= 5000万円 AND roi_months <= 24 | 30.0–49.9 | low |
| annual_saving_potential < 5000万円 OR roi_months > 24 | 10.0–29.9 | low |

```
base_score = lookup_from_table(annual_saving_potential, roi_months)

-- 調整係数
IF availability_date <= infra_refresh_plan_date THEN
    -- 更改計画に間に合う場合、スコアを上方調整
    adjustment = +10.0
END IF

IF cloud_provider_contracts.any(commitment_end_date <= availability_date + 90days) THEN
    -- クラウド契約の切り替えタイミングが近い場合
    adjustment = adjustment + 5.0
END IF

IF tech_obsolescence_risk = 'high' THEN
    adjustment = adjustment + 8.0
END IF

impact_score = CLAMP(base_score + adjustment, 0.0, 100.0)
```

### Step 4: 供給タイミング評価

```
availability_date = extracted_entities.shipment_start_date  -- 2026-Q4
cloud_availability_estimate = availability_date + 90days  -- クラウド提供は出荷後約3ヶ月

days_until_available = (cloud_availability_estimate - TODAY).days

IF days_until_available <= 90 THEN
    timing_action = 'immediate_planning'
ELIF days_until_available <= 180 THEN
    timing_action = 'advance_negotiation'
ELIF days_until_available <= 365 THEN
    timing_action = 'strategic_review'
ELSE
    timing_action = 'monitoring'
END IF
```

## 出力

### インパクト診断結果

| フィールド | 型 | 説明 |
|-----------|-----|------|
| impact_score | decimal(5,2) | インパクトスコア（0.00–100.00） |
| impact_level | string | インパクトレベル（critical / high / medium / low） |
| tech_obsolescence_risk | string | 技術陳腐化リスク（high / medium / low） |
| projected_cost_reduction_pct | decimal | 推定コスト削減率（%） |
| annual_saving_potential_jpy | decimal | 年間コスト削減ポテンシャル（円） |
| migration_cost_jpy | decimal | 移行コスト概算（円） |
| roi_months | int | 投資回収期間（月） |
| timing_action | string | タイミングアクション区分 |
| cloud_availability_estimate | date | クラウド提供開始見込み日 |

### 推奨アクション

| 優先度 | アクション | 担当 | 期限目安 |
|--------|-----------|------|----------|
| P1 | 現行推論基盤のコスト効率をB300スペックと比較分析 | インフラチーム | 2週間以内 |
| P1 | AWS/Azure/GCPのB300インスタンス提供時期・価格をヒアリング | クラウド調達担当 | 1ヶ月以内 |
| P2 | 次期インフラ更改計画へのB300導入シナリオ策定 | インフラチーム | 2ヶ月以内 |
| P2 | クラウドプロバイダとの価格交渉タイミング再検討 | 調達部 | 2ヶ月以内 |
| P3 | 電力効率改善による環境負荷低減レポート作成 | サステナビリティ推進室 | 3ヶ月以内 |
| P3 | B300対応モデル最適化の技術調査（FP4推論等） | AI研究開発チーム | 3ヶ月以内 |

### 通知設定

| 条件 | チャネル | 宛先 |
|------|---------|------|
| impact_level = 'high' AND timing_action = 'immediate_planning' | Teams (即時) | CTO, インフラ部長 |
| impact_level = 'high' | Teams (当日) | AI事業部マネージャー, インフラチーム |
| impact_level = 'medium' | Teams (当日) | インフラチーム, クラウド調達担当 |
| impact_level = 'low' | Email (週次) | AI事業部メンバー |

## 参照データソース

### Fabric テーブル

| レイクハウス | テーブル | 用途 |
|-------------|---------|------|
| lh_ai_agent_gold | products | 提供中AIモデル一覧・利用GPU情報 |
| lh_ai_agent_gold | contracts | クラウドプロバイダ契約情報・コミット期間 |
| lh_ai_agent_gold | transactions | 月間推論コスト・リクエスト数の集計 |
| lh_ai_agent_gold | infra_inventory | GPU保有台数・世代・消費電力情報 |
| lh_common_gold | unified_customers | 顧客規模（推論需要予測用） |
| lh_news_gold | news_articles | ニュース記事メタデータ |
| lh_news_gold | impact_analyses | 過去のテクノロジー系インパクト分析（トレンド比較用） |

### 外部参照

| ソース | 用途 |
|--------|------|
| NVIDIA公式プレスリリース | スペック・価格・出荷スケジュールの確認 |
| AWS/Azure/GCP インスタンス価格API | クラウドGPUインスタンスの現行価格取得 |
| 為替レートAPI | USD/JPY変換 |
| TrendForce / Mercury Research | 半導体供給予測・市場分析データ |
