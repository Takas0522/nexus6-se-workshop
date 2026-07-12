# 携帯電話事業：競合統合インパクト判断スキル

## 概要

主要競合他社の大型M&A・経営統合が携帯電話事業に与えるインパクトを判断するスキル。統合後の市場シェア変動、料金プラン競争力の変化、顧客流出リスクを定量評価し、対抗戦略の推奨アクションを出力する。

## 入力パラメータ

### ニュース情報

| パラメータ | 型 | 説明 |
|---|---|---|
| `news_category` | string | ニュースカテゴリ（`competition`） |
| `merging_companies` | list[string] | 統合する企業名リスト |
| `merged_market_share` | float | 統合後の推定市場シェア（0.0〜1.0） |
| `merger_stage` | enum | `rumor` / `announced` / `approved` / `completed` |
| `price_reduction_signal` | bool | 値下げ攻勢の兆候有無 |
| `estimated_synergy_amount` | int | 統合シナジー見込額（億円） |

### 業務データ

| パラメータ | 型 | 説明 |
|---|---|---|
| `own_market_share` | float | 自社市場シェア（0.0〜1.0） |
| `own_arpu` | int | 自社ARPU（円/月） |
| `competitor_arpu_avg` | int | 競合平均ARPU（円/月） |
| `churn_rate_monthly` | float | 月次解約率（0.0〜1.0） |
| `subscriber_count` | int | 自社契約者数 |
| `price_plan_count` | int | 現行料金プラン数 |
| `contract_lock_ratio` | float | 契約縛り有りユーザー比率（0.0〜1.0） |

## 判断ロジック

### Step 1: 市場集中度変化の評価

```
share_gap = merged_market_share - own_market_share
IF share_gap >= 0.20 THEN concentration_risk = "critical"
ELSE IF share_gap >= 0.10 THEN concentration_risk = "high"
ELSE IF share_gap >= 0.05 THEN concentration_risk = "medium"
ELSE concentration_risk = "low"
```

### Step 2: 価格競争リスクの算出

```
IF price_reduction_signal == true THEN
    price_pressure = (competitor_arpu_avg - own_arpu) / own_arpu
    IF price_pressure < -0.20 THEN price_risk = "critical"
    ELSE IF price_pressure < -0.10 THEN price_risk = "high"
    ELSE price_risk = "medium"
ELSE
    price_risk = "low"
```

### Step 3: 顧客流出リスクの算出

```
vulnerable_subscribers = subscriber_count × (1 - contract_lock_ratio)
churn_acceleration_factor =
    IF merger_stage == "completed" THEN 2.5
    ELSE IF merger_stage == "approved" THEN 2.0
    ELSE IF merger_stage == "announced" THEN 1.5
    ELSE 1.0

projected_monthly_churn = vulnerable_subscribers × churn_rate_monthly × churn_acceleration_factor
annual_churn_risk = projected_monthly_churn × 12
revenue_at_risk = annual_churn_risk × own_arpu × 12
```

### Step 4: 総合インパクトスコア算出

```
concentration_weight:
  critical = 1.0, high = 0.7, medium = 0.4, low = 0.1

price_weight:
  critical = 1.0, high = 0.7, medium = 0.4, low = 0.1

stage_weight:
  completed = 1.0, approved = 0.8, announced = 0.5, rumor = 0.2

raw_score = (concentration_weight × 0.4 + price_weight × 0.35 + (1 - contract_lock_ratio) × 0.25) × stage_weight × 100

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
| `revenue_at_risk_jpy` | int | 年間売上リスク額（円） |
| `projected_annual_churn` | int | 年間予測流出者数 |
| `share_gap` | float | 統合後シェア差 |
| `recommended_actions` | list[string] | 推奨アクションリスト |
| `risk_summary` | string | リスクサマリー文（自然言語） |

### 推奨アクション分岐

| レベル | 推奨アクション |
|---|---|
| critical | 対抗料金プランの緊急策定、顧客リテンション施策の即時発動（ポイント付与・契約更新特典）、経営層へのエスカレーション、M&A対抗戦略の検討開始 |
| high | 競合プラン分析の強化、サブブランド戦略の加速、通信品質差別化の訴求強化、解約予備軍への先制アプローチ |
| medium | 競合動向モニタリングの頻度引上げ、料金プラン改定シミュレーションの実施、顧客満足度調査の前倒し |
| low | 定期レポートへの記載、次回戦略会議でのアジェンダ追加 |

## 参照データソース（Fabric テーブル）

| テーブル名 | 説明 | 主要カラム |
|---|---|---|
| `fact_subscriber_contracts` | 契約者情報 | `subscriber_id`, `plan_id`, `contract_start`, `lock_end_date`, `status` |
| `fact_monthly_churn` | 月次解約実績 | `month`, `churn_count`, `churn_reason`, `destination_carrier` |
| `fact_revenue_monthly` | 月次売上実績 | `month`, `subscriber_id`, `revenue`, `arpu` |
| `dim_price_plans` | 料金プランマスタ | `plan_id`, `plan_name`, `monthly_fee`, `data_cap_gb`, `launch_date` |
| `dim_competitors` | 競合企業マスタ | `competitor_id`, `name`, `estimated_share`, `flagship_plan_price` |
| `fact_market_share_history` | 市場シェア推移 | `quarter`, `carrier_id`, `share`, `subscriber_count` |
| `fact_competitor_events` | 競合イベント履歴 | `event_date`, `competitor_id`, `event_type`, `description`, `impact_assessment` |
