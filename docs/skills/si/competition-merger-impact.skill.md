# SI事業：競合統合インパクト判断スキル

## 概要

主要競合SIベンダーの大型M&A・経営統合がSI事業に与えるインパクトを判断するスキル。統合後の技術者リソース規模拡大による入札競争力の変化、価格ダンピングリスク、既存クライアントの囲い込み・流出リスクを定量評価し、営業・提案戦略の見直しに関する推奨アクションを出力する。

## 入力パラメータ

### ニュース情報

| パラメータ | 型 | 説明 |
|---|---|---|
| `news_category` | string | ニュースカテゴリ（`competition`） |
| `merging_companies` | list[string] | 統合する企業名リスト |
| `merged_engineer_count` | int | 統合後の推定技術者数 |
| `merged_revenue` | int | 統合後の推定年間売上（億円） |
| `merged_market_share` | float | 統合後の推定SI市場シェア（0.0〜1.0） |
| `merger_stage` | enum | `rumor` / `announced` / `approved` / `completed` |
| `vertical_overlap` | list[string] | 統合企業の強み業界領域リスト |

### 業務データ

| パラメータ | 型 | 説明 |
|---|---|---|
| `own_market_share` | float | 自社SI市場シェア（0.0〜1.0） |
| `own_engineer_count` | int | 自社技術者数 |
| `active_bids_count` | int | 進行中の入札・提案件数 |
| `bid_win_rate` | float | 直近1年の入札勝率（0.0〜1.0） |
| `client_overlap_ratio` | float | 統合企業との顧客重複率（0.0〜1.0） |
| `top10_client_revenue_ratio` | float | 上位10社売上集中度（0.0〜1.0） |
| `avg_deal_size_oku` | float | 平均案件規模（億円） |
| `specialty_domains` | list[string] | 自社強み領域リスト |
| `annual_revenue_oku` | float | 年間売上（億円） |

## 判断ロジック

### Step 1: 規模格差の評価

```
engineer_ratio = merged_engineer_count / own_engineer_count
revenue_ratio = merged_revenue / annual_revenue_oku

IF engineer_ratio >= 5.0 OR revenue_ratio >= 5.0 THEN scale_threat = "critical"
ELSE IF engineer_ratio >= 3.0 OR revenue_ratio >= 3.0 THEN scale_threat = "high"
ELSE IF engineer_ratio >= 1.5 OR revenue_ratio >= 1.5 THEN scale_threat = "medium"
ELSE scale_threat = "low"
```

### Step 2: 入札競争力への影響算出

```
// 領域重複度の算出
domain_overlap = len(intersection(vertical_overlap, specialty_domains)) / len(specialty_domains)

// 入札勝率の低下予測
price_pressure_factor =
    IF scale_threat == "critical" THEN 0.30
    ELSE IF scale_threat == "high" THEN 0.20
    ELSE IF scale_threat == "medium" THEN 0.10
    ELSE 0.03

projected_win_rate_decline = bid_win_rate × price_pressure_factor × (0.5 + domain_overlap × 0.5)
new_projected_win_rate = bid_win_rate - projected_win_rate_decline

// 失注による売上影響
projected_lost_deals = active_bids_count × projected_win_rate_decline
projected_revenue_loss = projected_lost_deals × avg_deal_size_oku
```

### Step 3: 顧客流出リスクの算出

```
stage_multiplier:
  completed = 2.0
  approved = 1.5
  announced = 1.0
  rumor = 0.3

// 重複顧客ほど統合企業に引き寄せられやすい
client_churn_risk = client_overlap_ratio × top10_client_revenue_ratio × stage_multiplier

// 年間売上に対するリスク額
revenue_at_risk = annual_revenue_oku × client_churn_risk × 0.5
```

### Step 4: 総合インパクトスコア算出

```
scale_weight:
  critical = 1.0, high = 0.7, medium = 0.4, low = 0.1

stage_weight:
  completed = 1.0, approved = 0.8, announced = 0.5, rumor = 0.2

raw_score = (scale_weight × 0.30 + domain_overlap × 0.25 + client_churn_risk × 0.25 + projected_win_rate_decline × 2.0 × 0.20) × stage_weight × 100

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
| `projected_win_rate_decline` | float | 入札勝率低下幅 |
| `projected_lost_deals` | float | 予測失注件数 |
| `projected_revenue_loss_oku` | float | 予測売上減少額（億円） |
| `revenue_at_risk_oku` | float | 顧客流出による売上リスク額（億円） |
| `domain_overlap` | float | 領域重複度 |
| `recommended_actions` | list[string] | 推奨アクションリスト |
| `risk_summary` | string | リスクサマリー文（自然言語） |

### 推奨アクション分岐

| レベル | 推奨アクション |
|---|---|
| critical | 重要顧客への即時訪問・リレーション強化、差別化領域の緊急特定と提案力強化、戦略的価格見直し（利益率を一時的に犠牲にした防衛入札）、技術者採用・パートナーシップの加速、経営層へのエスカレーション・対抗M&A検討 |
| high | 専門特化領域でのブランディング強化、既存顧客向けバリューアップ提案の実施、共同入札・アライアンス先の探索、重複領域の競争力分析と投資判断 |
| medium | 競合統合動向の継続モニタリング、入札案件のパイプライン精査と優先順位見直し、自社強み領域の棚卸しと訴求資料更新 |
| low | 定期レポートへの記載、次回営業戦略会議でのアジェンダ追加 |

## 参照データソース（Fabric テーブル）

| テーブル名 | 説明 | 主要カラム |
|---|---|---|
| `fact_bids` | 入札・提案実績 | `bid_id`, `project_name`, `client_id`, `bid_amount`, `status`, `competitor_ids`, `result` |
| `fact_project_revenue` | プロジェクト売上実績 | `project_id`, `client_id`, `quarter`, `revenue_oku`, `margin_rate` |
| `fact_client_transactions` | 顧客取引履歴 | `client_id`, `fiscal_year`, `total_revenue`, `project_count`, `relationship_years` |
| `dim_clients` | 顧客マスタ | `client_id`, `name`, `industry`, `tier`, `primary_contact`, `competitor_relationship` |
| `dim_competitors` | 競合企業マスタ | `competitor_id`, `name`, `engineer_count`, `revenue_oku`, `specialty_domains`, `market_share` |
| `fact_market_share_history` | SI市場シェア推移 | `quarter`, `company_id`, `share`, `revenue_oku`, `deal_count` |
| `fact_engineer_capacity` | 技術者キャパシティ | `month`, `domain`, `available_count`, `utilization_rate`, `skill_level` |
