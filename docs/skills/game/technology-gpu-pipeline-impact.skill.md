# スキル: 次世代GPUによるゲーム開発・配信パイプラインインパクト分析

## 概要

GPU・グラフィックス技術の新製品発表ニュースを分析し、当社ゲーム事業の開発パイプライン（グラフィックスエンジン、物理シミュレーション、ゲームAI）、プレイヤー体験、ハードウェア市場動向への影響を判断するスキル。新GPUの性能特性と当社タイトルの技術要件を照合し、対応優先度・マーケティング機会・QA計画への影響を算出する。

## 入力パラメータ

### ニュース情報

| パラメータ | 型 | 説明 |
|-----------|-----|------|
| article_id | string | ニュース記事ID |
| title | string | 記事タイトル |
| summary | string | 記事要約 |
| category | string | カテゴリ（technology） |
| published_at | datetime | 配信日時 |
| extracted_entities | object[] | 抽出エンティティ（GPU名、性能倍率、価格、出荷時期、対応機能、コンシューマ向け情報等） |

### 業務データ

| パラメータ | 型 | 説明 | ソース |
|-----------|-----|------|--------|
| active_titles | object[] | 運営中タイトル一覧（エンジン、対応プラットフォーム） | sqldb_game_01.game_titles |
| upcoming_titles | object[] | 開発中タイトル一覧（リリース予定日、対応GPU世代） | 開発管理システム |
| current_gpu_requirements | object[] | タイトル別推奨GPU要件 | 技術台帳 |
| pc_player_ratio | float | PC版プレイヤー比率 | sqldb_game_01.customers (platform='pc') |
| pc_player_gpu_distribution | object[] | PCプレイヤーのGPU分布 | テレメトリデータ |
| cloud_gaming_active_users | int | クラウドゲーミング利用者数 | sqldb_game_01.customers |
| monthly_revenue_by_platform | object[] | プラットフォーム別月間売上 | sqldb_game_01.transactions |
| qa_test_environment_gpus | object[] | QA環境のGPU構成 | インフラ台帳 |
| graphics_engine_version | object[] | 使用中グラフィックスエンジンバージョン | 技術台帳 |

## 判断ロジック

### Step 1: コンシューマGPU世代交代インパクト評価

```
new_consumer_gpu = extracted_entities.consumer_gpu  -- "GeForce RTX 6090"
consumer_launch_date = extracted_entities.consumer_launch_date  -- 2027-Q1
consumer_performance_gain = extracted_entities.consumer_performance_ratio  -- 1.6x (RT性能)

-- 新GPUリリースまでの猶予期間
days_until_consumer_launch = (consumer_launch_date - TODAY).days

-- 開発中タイトルとの整合性チェック
titles_launching_after_gpu = upcoming_titles.filter(release_date >= consumer_launch_date)
titles_needing_optimization = titles_launching_after_gpu.filter(
    target_gpu_generation < new_consumer_gpu.generation
)

-- PC版売上への影響見積り
pc_revenue_monthly = monthly_revenue_by_platform.filter(platform='pc').sum()
pc_revenue_annual = pc_revenue_monthly * 12
```

### Step 2: 技術的対応必要度の算出

```
-- レイトレーシング性能向上による品質期待値上昇
IF consumer_performance_gain >= 1.5 THEN
    graphical_expectation_shift = 'significant'
    -- プレイヤーが新GPU性能に見合うグラフィックス品質を期待
ELIF consumer_performance_gain >= 1.2 THEN
    graphical_expectation_shift = 'moderate'
ELSE
    graphical_expectation_shift = 'minimal'
END IF

-- エンジン対応状況
FOR EACH title IN active_titles + upcoming_titles:
    engine = graphics_engine_version.find(title.id)
    IF engine.supports_new_gpu_features = TRUE THEN
        title.engine_readiness = 'ready'
    ELIF engine.can_update_to_support = TRUE THEN
        title.engine_readiness = 'update_needed'
    ELSE
        title.engine_readiness = 'major_work_required'
    END IF
END FOR

titles_needing_engine_update = count(titles WHERE engine_readiness != 'ready')
titles_major_work = count(titles WHERE engine_readiness = 'major_work_required')
```

### Step 3: データセンターGPUによるクラウドゲーミング影響

```
-- B300のデータセンター性能がクラウドゲーミング品質に与える影響
dc_performance_gain = extracted_entities.dc_performance_multiplier  -- 2.5x
dc_efficiency_gain = extracted_entities.dc_efficiency_multiplier  -- 1.8x

-- クラウドゲーミングのコスト効率改善見込み
cloud_gaming_cost_reduction_ratio = 1 - (1 / dc_efficiency_gain)  -- 44.4%
cloud_gaming_quality_improvement = dc_performance_gain  -- レンダリング品質向上

-- クラウドゲーミング事業機会
IF cloud_gaming_active_users > 0 THEN
    cloud_gaming_opportunity = cloud_gaming_active_users * cloud_gaming_cost_reduction_ratio
    cloud_gaming_expansion_potential = 'high'
ELSE
    cloud_gaming_opportunity = 0
    cloud_gaming_expansion_potential = 'assessment_needed'
END IF
```

### Step 4: インパクトスコア算出

| 条件 | スコア範囲 | レベル |
|------|-----------|--------|
| titles_needing_optimization >= 3 AND graphical_expectation_shift = 'significant' | 65.0–85.0 | high |
| titles_needing_optimization >= 1 OR titles_needing_engine_update >= 2 | 40.0–64.9 | medium |
| graphical_expectation_shift = 'moderate' AND no titles affected | 20.0–39.9 | low |
| graphical_expectation_shift = 'minimal' | 5.0–19.9 | low |

```
base_score = lookup_from_table(titles_needing_optimization, graphical_expectation_shift)

-- 調整係数
IF pc_player_ratio >= 0.40 THEN
    -- PC版比率が高い場合、GPU世代交代の影響大
    adjustment = +10.0
ELIF pc_player_ratio >= 0.20 THEN
    adjustment = +5.0
ELSE
    adjustment = +0.0
END IF

IF cloud_gaming_expansion_potential = 'high' THEN
    adjustment = adjustment + 5.0
END IF

IF titles_major_work >= 1 THEN
    adjustment = adjustment + 8.0
END IF

-- タイミング調整（リリース直前のタイトルがある場合）
titles_in_crunch = upcoming_titles.filter(
    release_date BETWEEN consumer_launch_date - 60days AND consumer_launch_date + 90days
)
IF titles_in_crunch.count >= 1 THEN
    adjustment = adjustment + 7.0  -- Day-1対応が必要
END IF

impact_score = CLAMP(base_score + adjustment, 0.0, 100.0)
```

## 出力

### インパクト診断結果

| フィールド | 型 | 説明 |
|-----------|-----|------|
| impact_score | decimal(5,2) | インパクトスコア（0.00–100.00） |
| impact_level | string | インパクトレベル（high / medium / low） |
| titles_needing_optimization | int | GPU最適化が必要なタイトル数 |
| titles_needing_engine_update | int | エンジン更新が必要なタイトル数 |
| graphical_expectation_shift | string | グラフィックス期待値変化（significant / moderate / minimal） |
| cloud_gaming_cost_reduction_pct | decimal | クラウドゲーミングコスト削減率（%） |
| pc_revenue_at_risk_jpy | decimal | 最適化未対応時のPC版売上リスク額（円） |
| consumer_gpu_launch_date | date | コンシューマGPU発売予定日 |
| days_until_consumer_launch | int | 発売までの日数 |

### 推奨アクション

| 優先度 | アクション | 担当 | 期限目安 |
|--------|-----------|------|----------|
| P1 | RTX 6090スペックに基づく対応タイトルの技術要件見直し | テクニカルディレクター | 3週間以内 |
| P1 | 開発中タイトルのGPU最適化ロードマップ更新 | 各タイトル開発リード | 1ヶ月以内 |
| P2 | グラフィックスエンジンの次世代GPU対応アップデート計画策定 | エンジンチーム | 2ヶ月以内 |
| P2 | QA環境への新GPU導入計画・予算確保 | QAマネージャー, インフラ | 2ヶ月以内 |
| P2 | クラウドゲーミング基盤のB300移行による品質向上・コスト削減試算 | クラウドゲーミングチーム | 2ヶ月以内 |
| P3 | 新GPU発売に合わせたマーケティングキャンペーン企画（対応タイトルPR） | マーケティング部 | 4ヶ月以内 |

### 通知設定

| 条件 | チャネル | 宛先 |
|------|---------|------|
| impact_level = 'high' AND days_until_consumer_launch <= 180 | Teams (即時) | CTO, テクニカルディレクター全員 |
| impact_level = 'high' | Teams (当日) | 各タイトルプロデューサー, エンジンチームリード |
| impact_level = 'medium' | Teams (当日) | 開発部門マネージャー |
| impact_level = 'low' | Email (週次) | ゲーム事業部メンバー |

## 参照データソース

### Fabric テーブル

| レイクハウス | テーブル | 用途 |
|-------------|---------|------|
| lh_game_gold | game_titles | 運営中・開発中タイトル一覧、対応プラットフォーム |
| lh_game_gold | customers | プラットフォーム別プレイヤー数・GPU分布 |
| lh_game_gold | transactions | プラットフォーム別売上集計 |
| lh_game_gold | inventory | デジタルコンテンツ・DLC配信状況 |
| lh_common_gold | unified_customers | 統合顧客情報 |
| lh_news_gold | news_articles | ニュース記事メタデータ |
| lh_news_gold | impact_analyses | 過去のテクノロジー系インパクト分析 |

### 外部参照

| ソース | 用途 |
|--------|------|
| NVIDIA公式プレスリリース | RTX 6090スペック・発売日・対応機能の確認 |
| Steam Hardware Survey | PCゲーマーのGPU分布・世代交代速度の参照 |
| Unreal/Unity エンジン対応ロードマップ | エンジンの新GPU対応時期 |
| 為替レートAPI | USD/JPY変換（QA機器調達コスト算出用） |
