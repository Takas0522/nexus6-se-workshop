# SNS事業：競合統合インパクト判断スキル

## 概要

主要競合SNSプラットフォームの大型M&A・経営統合がSNS事業に与えるインパクトを判断するスキル。統合後のユーザー基盤拡大による自社ユーザー流出リスク、広告市場の寡占化による広告単価変動、データ統合によるターゲティング精度格差を定量評価し、対抗戦略の推奨アクションを出力する。

## 入力パラメータ

### ニュース情報

| パラメータ | 型 | 説明 |
|---|---|---|
| `news_category` | string | ニュースカテゴリ（`competition`） |
| `merging_companies` | list[string] | 統合する企業名リスト |
| `merged_mau` | int | 統合後の推定MAU（月間アクティブユーザー数） |
| `merged_ad_market_share` | float | 統合後の広告市場シェア（0.0〜1.0） |
| `merger_stage` | enum | `rumor` / `announced` / `approved` / `completed` |
| `data_integration_planned` | bool | ユーザーデータ統合の計画有無 |
| `overlap_user_ratio` | float | 自社とのユーザー重複率（0.0〜1.0） |

### 業務データ

| パラメータ | 型 | 説明 |
|---|---|---|
| `own_mau` | int | 自社MAU |
| `own_dau_mau_ratio` | float | DAU/MAU比率（エンゲージメント指標） |
| `own_ad_market_share` | float | 自社広告市場シェア（0.0〜1.0） |
| `avg_ad_unit_price` | int | 自社平均広告単価（円/imp） |
| `ad_revenue_monthly` | int | 月間広告収入（円） |
| `user_growth_rate_monthly` | float | 月次ユーザー成長率 |
| `creator_count` | int | アクティブクリエイター数 |
| `exclusive_content_ratio` | float | 独占コンテンツ比率（0.0〜1.0） |

## 判断ロジック

### Step 1: ユーザー基盤格差の評価

```
mau_ratio = merged_mau / own_mau
IF mau_ratio >= 5.0 THEN user_dominance = "critical"
ELSE IF mau_ratio >= 3.0 THEN user_dominance = "high"
ELSE IF mau_ratio >= 1.5 THEN user_dominance = "medium"
ELSE user_dominance = "low"
```

### Step 2: ユーザー流出リスクの算出

```
// 重複ユーザーほど統合プラットフォームへ移行しやすい
base_churn_risk = overlap_user_ratio × (1 - own_dau_mau_ratio)

stage_multiplier:
  completed = 2.0
  approved = 1.5
  announced = 1.0
  rumor = 0.4

content_retention_factor = 1.0 - (exclusive_content_ratio × 0.5)

projected_user_loss_rate = base_churn_risk × stage_multiplier × content_retention_factor
projected_user_loss = own_mau × projected_user_loss_rate
```

### Step 3: 広告市場への影響算出

```
ad_share_gap = merged_ad_market_share - own_ad_market_share

// 統合によるターゲティング精度向上の脅威
IF data_integration_planned == true THEN
    targeting_threat = 0.3
ELSE
    targeting_threat = 0.1

// 広告単価への下落圧力（寡占化による買い手交渉力低下 vs 出稿先集中）
ad_price_pressure = ad_share_gap × targeting_threat × -1
projected_ad_revenue_impact = ad_revenue_monthly × (projected_user_loss_rate + ad_price_pressure) × 12
```

### Step 4: 総合インパクトスコア算出

```
dominance_weight:
  critical = 1.0, high = 0.7, medium = 0.4, low = 0.1

stage_weight:
  completed = 1.0, approved = 0.8, announced = 0.5, rumor = 0.2

raw_score = (dominance_weight × 0.35 + projected_user_loss_rate × 2.0 × 0.35 + ad_share_gap × 0.30) × stage_weight × 100

impact_score = clamp(raw_score, 0, 100)
```

### Step 5: インパクトレベル判定

```
IF impact_score >= 80 THEN level = "critical"
ELSE IF impact_score >= 55 THEN level = "high"
ELSE IF impact_score >= 30 THEN level = "medium"
ELSE level = "low"
```

## 出力

| フィールド | 型 | 説明 |
|---|---|---|
| `impact_score` | int (0-100) | 総合インパクトスコア |
| `impact_level` | enum | `critical` / `high` / `medium` / `low` |
| `projected_user_loss` | int | 予測ユーザー流出数 |
| `projected_user_loss_rate` | float | 予測ユーザー流出率 |
| `projected_ad_revenue_impact_jpy` | int | 年間広告収入影響額（円） |
| `ad_share_gap` | float | 広告市場シェア格差 |
| `recommended_actions` | list[string] | 推奨アクションリスト |
| `risk_summary` | string | リスクサマリー文（自然言語） |

### 推奨アクション分岐

| レベル | 推奨アクション |
|---|---|
| critical | クリエイター独占契約の緊急強化、ユーザーリテンション施策の即時発動（特典・限定機能解放）、広告主向け差別化提案の緊急策定、経営層へのエスカレーション、提携・対抗M&Aの検討開始 |
| high | 独自機能・コンテンツの開発加速、クリエイター報酬プログラムの拡充、広告ターゲティング精度の向上投資、エンゲージメント強化キャンペーンの実施 |
| medium | ユーザー行動変化のモニタリング強化、競合統合プラットフォームのベンチマーク分析、コンテンツ戦略の見直し検討 |
| low | 定期レポートへの記載、次回プロダクト戦略会議でのアジェンダ追加 |

## 参照データソース（Fabric テーブル）

| テーブル名 | 説明 | 主要カラム |
|---|---|---|
| `fact_user_activity` | ユーザーアクティビティ | `date`, `user_id`, `session_count`, `time_spent_min`, `actions_count` |
| `fact_user_churn` | ユーザー離脱実績 | `month`, `user_id`, `churn_date`, `churn_reason`, `destination_platform` |
| `fact_ad_revenue` | 広告収入実績 | `date`, `advertiser_id`, `campaign_id`, `impressions`, `revenue_jpy`, `unit_price` |
| `fact_creator_metrics` | クリエイター指標 | `month`, `creator_id`, `followers`, `posts_count`, `engagement_rate`, `exclusive_flag` |
| `dim_competitors` | 競合プラットフォームマスタ | `competitor_id`, `name`, `estimated_mau`, `ad_market_share`, `primary_category` |
| `fact_market_share_history` | 市場シェア推移 | `quarter`, `platform_id`, `mau`, `ad_share`, `revenue_share` |
| `agg_engagement_daily` | 日次エンゲージメント集計 | `date`, `dau`, `mau`, `avg_session_min`, `retention_d7`, `retention_d30` |
