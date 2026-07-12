# SNS事業：5G帯域拡張に伴うリッチコンテンツ需要インパクト判断スキル

## 概要

5G周波数帯の追加割当（3.8GHz帯）によるモバイル通信環境の高速化・大容量化が、SNS事業のコンテンツ消費パターン・インフラ設計・広告フォーマットに与えるインパクトを判断するスキル。動画・ライブ配信・AR/VRコンテンツの利用増加に伴うトラフィック設計の見直し、広告フォーマットの高度化機会、CDN/インフラコストへの影響を評価し、中期的な事業機会とインフラ投資の推奨アクションを出力する。

## 入力パラメータ

### ニュース情報

| パラメータ | 型 | 説明 |
|---|---|---|
| `news_category` | string | ニュースカテゴリ（`regulation`） |
| `frequency_band_mhz` | string | 割当対象周波数帯（例: `3800-3920`） |
| `bandwidth_mhz` | int | 割当帯域幅（MHz） |
| `traffic_growth_rate_percent` | float | 5Gトラフィック増加率（%） |
| `estimated_industry_capex_jpy` | int | 業界全体の設備投資試算額（円） |
| `coverage_expansion_timeline_months` | int | カバレッジ拡大までの想定期間（月） |
| `theoretical_speed_improvement_percent` | float | 理論通信速度向上率（%）（帯域拡張による） |

### 業務データ

| パラメータ | 型 | 説明 |
|---|---|---|
| `total_mau` | int | 月間アクティブユーザー数（MAU） |
| `mobile_user_ratio_percent` | float | モバイルアクセス比率（%） |
| `video_content_ratio_percent` | float | 動画コンテンツの全投稿に占める比率（%） |
| `live_streaming_dau` | int | ライブ配信日間アクティブユーザー数 |
| `avg_video_bitrate_mbps` | float | 平均動画配信ビットレート（Mbps） |
| `peak_concurrent_streams` | int | ピーク時同時配信数 |
| `monthly_bandwidth_cost_jpy` | int | 月間帯域コスト（円） |
| `cdn_provider_count` | int | CDNプロバイダー数 |
| `ad_video_format_ratio_percent` | float | 動画広告の全広告に占める比率（%） |
| `ad_interactive_format_available` | bool | インタラクティブ広告（AR等）の提供有無 |
| `avg_ad_cpm_video_jpy` | float | 動画広告の平均CPM（円） |
| `avg_ad_cpm_static_jpy` | float | 静的広告の平均CPM（円） |
| `infra_capacity_headroom_percent` | float | 現行インフラの余裕率（%） |
| `content_quality_tiers` | list[string] | 対応画質（例: `720p`, `1080p`, `4K`, `8K`） |

## 判断ロジック

### Step 1: ユーザー行動変化の予測

```
-- 5G帯域拡張に伴うリッチコンテンツ消費の増加予測
-- 通信速度向上によるユーザー行動変化係数
IF theoretical_speed_improvement_percent >= 50 THEN
    behavior_shift_factor = 1.4     -- 大幅な行動変化（4K動画が標準化等）
ELSE IF theoretical_speed_improvement_percent >= 30 THEN
    behavior_shift_factor = 1.25    -- 中程度の変化
ELSE IF theoretical_speed_improvement_percent >= 15 THEN
    behavior_shift_factor = 1.12    -- 軽微な変化
ELSE
    behavior_shift_factor = 1.05    -- ほぼ影響なし

-- 動画コンテンツ比率の将来予測（18ヶ月後）
projected_video_ratio = MIN(video_content_ratio_percent × behavior_shift_factor, 85)
video_ratio_increase = projected_video_ratio - video_content_ratio_percent

-- ライブ配信ユーザーの増加予測
projected_live_dau = live_streaming_dau × (1 + traffic_growth_rate_percent / 100 × 0.4)
-- 5Gトラフィック増加率の40%がライブ配信に帰属すると仮定

-- ユーザー影響規模
affected_users = total_mau × (mobile_user_ratio_percent / 100)
```

### Step 2: インフラ負荷の影響評価

```
-- 帯域消費量の増加予測
avg_bitrate_projected = avg_video_bitrate_mbps × behavior_shift_factor
bandwidth_increase_ratio = avg_bitrate_projected / avg_video_bitrate_mbps

-- ピーク時負荷の予測
projected_peak_streams = peak_concurrent_streams × (1 + traffic_growth_rate_percent / 200)
projected_peak_bandwidth_gbps = projected_peak_streams × avg_bitrate_projected / 1000

-- インフラ余裕の充足判定
required_headroom_increase_percent = (bandwidth_increase_ratio - 1) × 100
IF required_headroom_increase_percent > infra_capacity_headroom_percent THEN
    infra_impact = "capacity_exceeded"    -- 現行キャパシティ超過
    capacity_gap_percent = required_headroom_increase_percent - infra_capacity_headroom_percent
ELSE IF required_headroom_increase_percent > infra_capacity_headroom_percent × 0.7 THEN
    infra_impact = "near_capacity"        -- 余裕が大幅に縮小
    capacity_gap_percent = 0
ELSE
    infra_impact = "within_capacity"      -- 現行で吸収可能
    capacity_gap_percent = 0

-- コスト影響の推定
additional_monthly_bandwidth_cost = monthly_bandwidth_cost_jpy × (bandwidth_increase_ratio - 1)
annual_infra_cost_increase = additional_monthly_bandwidth_cost × 12
```

### Step 3: 広告収益機会の評価

```
-- 動画広告のCPMプレミアム
video_cpm_premium = avg_ad_cpm_video_jpy / avg_ad_cpm_static_jpy

-- 動画広告比率の増加による収益機会
current_video_ad_revenue_weight = ad_video_format_ratio_percent / 100 × video_cpm_premium
projected_video_ad_ratio = MIN(ad_video_format_ratio_percent × behavior_shift_factor, 80)
projected_video_ad_revenue_weight = projected_video_ad_ratio / 100 × video_cpm_premium

revenue_uplift_ratio = projected_video_ad_revenue_weight / current_video_ad_revenue_weight
IF revenue_uplift_ratio > 1.3 THEN
    ad_opportunity = "significant"      -- 30%超の収益アップポテンシャル
ELSE IF revenue_uplift_ratio > 1.15 THEN
    ad_opportunity = "moderate"         -- 15-30%のポテンシャル
ELSE IF revenue_uplift_ratio > 1.05 THEN
    ad_opportunity = "marginal"         -- 5-15%
ELSE
    ad_opportunity = "minimal"

-- インタラクティブ広告の新規機会
IF NOT ad_interactive_format_available AND theoretical_speed_improvement_percent >= 30 THEN
    interactive_ad_opportunity = "new_market"    -- AR/VR広告の新規投入が可能に
ELSE IF ad_interactive_format_available THEN
    interactive_ad_opportunity = "expansion"     -- 既存の拡張
ELSE
    interactive_ad_opportunity = "premature"     -- 時期尚早
```

### Step 4: コンテンツ品質進化の機会評価

```
-- 新画質ティアの提供可能性
IF "4K" NOT IN content_quality_tiers AND theoretical_speed_improvement_percent >= 40 THEN
    quality_upgrade_opportunity = "4k_introduction"
    quality_competitive_advantage = "high"
ELSE IF "8K" NOT IN content_quality_tiers AND "4K" IN content_quality_tiers THEN
    quality_upgrade_opportunity = "8k_experimental"
    quality_competitive_advantage = "medium"
ELSE
    quality_upgrade_opportunity = "incremental"
    quality_competitive_advantage = "low"

-- ライブ配信の高画質化による差別化
IF live_streaming_dau > total_mau × 0.05 THEN
    live_hd_opportunity = "high"        -- ライブ配信が活発、高画質化の効果大
ELSE
    live_hd_opportunity = "moderate"
```

### Step 5: 時間軸の評価

```
-- カバレッジ拡大のフェーズに合わせた対応タイミング
IF coverage_expansion_timeline_months <= 6 THEN
    timing_urgency = "immediate"        -- 半年以内に影響開始
ELSE IF coverage_expansion_timeline_months <= 12 THEN
    timing_urgency = "near_term"        -- 1年以内
ELSE IF coverage_expansion_timeline_months <= 24 THEN
    timing_urgency = "medium_term"      -- 2年以内
ELSE
    timing_urgency = "long_term"        -- 2年超

-- 段階的な影響展開（都市部から地方へ）
-- 都市部ユーザー比率が高いSNSでは早期に影響
urban_user_ratio_assumed = 0.7  -- SNSユーザーの70%が都市部と仮定
early_impact_user_count = affected_users × urban_user_ratio_assumed
```

### Step 6: 総合インパクトスコアの算出

```
-- 機会スコア（ポジティブインパクト）
ad_opportunity_score = {"significant": 25, "moderate": 18, "marginal": 10, "minimal": 3}[ad_opportunity]
quality_score = {"high": 15, "medium": 10, "low": 5}[quality_competitive_advantage]
interactive_score = {"new_market": 15, "expansion": 10, "premature": 3}[interactive_ad_opportunity]

-- リスク/コストスコア
infra_score = {"capacity_exceeded": 20, "near_capacity": 12, "within_capacity": 3}[infra_impact]
timing_score = {"immediate": 15, "near_term": 10, "medium_term": 6, "long_term": 2}[timing_urgency]

-- 総合スコア（機会とリスクの混合）
total_impact_score = ad_opportunity_score + quality_score + interactive_score + infra_score + timing_score
total_impact_score = MIN(total_impact_score, 100)

-- インパクトレベルの決定
IF total_impact_score >= 70 THEN impact_level = "high"
ELSE IF total_impact_score >= 45 THEN impact_level = "medium"
ELSE IF total_impact_score >= 25 THEN impact_level = "low"
ELSE impact_level = "minimal"

-- 本シナリオは中期的な機会であり、通常criticalには達しない
-- ただしインフラ超過が発生する場合のみcriticalとする
IF infra_impact == "capacity_exceeded" AND timing_urgency == "immediate" THEN
    impact_level = "critical"
```

## 出力

### インパクト評価結果

| 出力項目 | 型 | 説明 |
|---|---|---|
| `impact_level` | enum | `critical` / `high` / `medium` / `low` / `minimal` |
| `impact_score` | int | 総合インパクトスコア（0-100） |
| `confidence_score` | float | 分析確信度（0.00-1.00） |
| `impact_type` | enum | `opportunity` / `risk` / `mixed` |
| `affected_user_count` | int | 影響を受けるモバイルユーザー数 |
| `projected_video_ratio_percent` | float | 18ヶ月後の動画コンテンツ比率予測（%） |
| `annual_infra_cost_increase_jpy` | int | 年間インフラコスト増加額（円） |
| `ad_revenue_uplift_ratio` | float | 広告収益向上倍率 |
| `quality_upgrade_opportunity` | string | 画質進化の機会 |
| `capacity_gap_percent` | float | インフラキャパシティ不足率（%） |

### 推奨アクション

| 優先度 | アクション | 条件 |
|---|---|---|
| P1（1ヶ月以内） | CDNキャパシティプランニングの見直し・増強計画策定 | `infra_impact` が `capacity_exceeded` または `near_capacity` |
| P1（1ヶ月以内） | 動画広告フォーマットの高画質対応（4K広告素材の受入開始） | `ad_opportunity` が `significant` |
| P2（3ヶ月以内） | AR/VRインタラクティブ広告フォーマットの開発・ベータ提供 | `interactive_ad_opportunity` が `new_market` |
| P2（3ヶ月以内） | ライブ配信の高画質ティア（1080p/4K）の提供開始 | `live_hd_opportunity` が `high` |
| P2（3ヶ月以内） | 動画コンテンツのアダプティブビットレート配信の最適化 | `behavior_shift_factor` が 1.2 以上 |
| P3（6ヶ月以内） | コンテンツクリエイター向け高画質アップロード・編集機能の強化 | `quality_upgrade_opportunity` が `4k_introduction` |
| P3（6ヶ月以内） | 5G対応リッチコンテンツ体験のプロモーション企画 | `timing_urgency` が `near_term` 以内 |
| P3（6ヶ月以内） | 広告主向け新フォーマット（動画+インタラクティブ）のメディアキット作成 | 常時実行 |

### 通知テンプレート

```
【{priority}】5G帯域拡張に伴うリッチコンテンツ需要変化の影響分析

■ インパクトレベル: {impact_level}（スコア: {impact_score}/100）
■ インパクト種別: {impact_type}
■ 影響ユーザー数: {affected_user_count:,}名
■ 動画比率予測（18ヶ月後）: {projected_video_ratio_percent:.1f}%（現行: {video_content_ratio_percent}%）
■ インフラコスト増: {annual_infra_cost_increase_jpy:,}円/年

5G周波数帯の追加割当（{bandwidth_mhz}MHz幅）が発表されました。
モバイル通信の高速化により、以下の変化が予測されます:

【機会】
- 動画広告CPMプレミアムの拡大（収益向上倍率: {ad_revenue_uplift_ratio:.2f}x）
- 高画質コンテンツによる差別化: {quality_upgrade_opportunity}
- インタラクティブ広告市場: {interactive_ad_opportunity}

【対応課題】
- インフラ状況: {infra_impact}
- キャパシティギャップ: {capacity_gap_percent:.1f}%

【推奨アクション】
{recommended_actions}

中長期のプロダクトロードマップへの反映を検討してください。
```

## 参照データソース

### Fabric テーブル（sqldb_sns_01）

| テーブル名 | 用途 |
|---|---|
| `customers` | MAU算出、アカウントティア別のユーザー行動分析（premium/businessユーザーのリッチコンテンツ利用度） |
| `contracts` | 広告主契約の動画広告比率分析、CPM単価の集計 |
| `transactions` | 広告費取引の種別分析（動画 vs 静的）、in_app_purchase（ライブギフト等）のトレンド |
| `ad_inventory` | 動画広告枠の在庫状況、CPM推移、フォーマット別のインプレッション分析 |

### Fabric テーブル（sqldb_common_01）

| テーブル名 | 用途 |
|---|---|
| `unified_customers` | ユーザーの地域分布（都市部/地方）の推定 |
| `customer_segments` | セグメント別のコンテンツ消費傾向分析 |

### Fabric テーブル（sqldb_news_01）

| テーブル名 | 用途 |
|---|---|
| `news_articles` | 5G・通信インフラ関連ニュースの時系列追跡 |
| `impact_analyses` | 携帯電話事業側の5G分析結果との整合確認 |
| `notifications` | 過去の関連通知との重複排除 |

### 外部参照データ

| データソース | 用途 |
|---|---|
| Akamai / Cloudflare トラフィックレポート | グローバルな動画トラフィックトレンドのベンチマーク |
| eMarketer デジタル広告レポート | 動画広告市場の成長率・CPMトレンド |
| 総務省 情報通信白書 | 国内5G普及率・トラフィック統計 |
