# スキル: 通信インフラ政策によるAIサービス到達圏拡大分析

## 概要

国内通信インフラ整備に関する政策ニュース（光ファイバー網拡充、5G展開等）を分析し、当社AIエージェント事業のサービス到達圏拡大・潜在顧客増加・エッジ推論需要への影響を判断するスキル。新規整備予定地域の人口・事業所データと当社顧客分布を照合し、中長期的な事業機会の規模を算出する。

## 入力パラメータ

### ニュース情報

| パラメータ | 型 | 説明 |
|-----------|-----|------|
| article_id | string | ニュース記事ID |
| title | string | 記事タイトル |
| summary | string | 記事要約 |
| category | string | カテゴリ（policy_infra） |
| published_at | datetime | 配信日時 |
| extracted_entities | object[] | 抽出エンティティ（予算額、対象世帯数、カバー率目標、整備完了予定日、補助率等） |

### 業務データ

| パラメータ | 型 | 説明 | ソース |
|-----------|-----|------|--------|
| total_customers_japan | int | 国内顧客数 | sqldb_ai_agent_01.customers (country_code='JP') |
| customers_by_region | object[] | 地域別顧客分布 | sqldb_ai_agent_01.customers GROUP BY region |
| monthly_api_calls_by_region | object[] | 地域別月間APIコール数 | sqldb_ai_agent_01.transactions GROUP BY region |
| current_latency_by_region | object[] | 地域別平均レイテンシ（ms） | モニタリングシステム |
| edge_deployment_count | int | エッジ推論ノード設置数 | インフラ台帳 |
| underserved_region_leads | int | 未整備地域からの問合せ・リード数 | CRMデータ |
| arpu_japan | decimal | 国内ARPU（円） | sqldb_ai_agent_01.transactions (集計) |
| api_call_growth_rate_monthly | float | 月間APIコール成長率（%） | 算出値 |

## 判断ロジック

### Step 1: 事業機会規模の算出

```
newly_covered_households = extracted_entities.target_households  -- 520,000世帯
target_coverage_rate = extracted_entities.target_coverage  -- 99.9%
completion_date = extracted_entities.completion_date  -- 2028年度末

-- 新規整備地域の事業所推定（世帯数の5%を事業所と仮定）
estimated_new_businesses = newly_covered_households * 0.05  -- 26,000事業所

-- AIサービス普及率を適用（現行市場浸透率ベース）
ai_service_penetration_rate = total_customers_japan / total_addressable_market_japan
potential_new_customers = estimated_new_businesses * ai_service_penetration_rate

-- 潜在売上増加額
potential_annual_revenue_increase = potential_new_customers * arpu_japan * 12
```

### Step 2: エッジ推論需要評価

```
-- 地方部の低レイテンシ需要増加予測
rural_regions_improved = extract_rural_regions(extracted_entities.target_areas)

-- 5Gガイドライン緩和によるエッジ需要増
IF extracted_entities.includes_5g_guideline_change = TRUE THEN
    edge_demand_multiplier = 1.3  -- ローカル5G普及でエッジ需要30%増見込み
ELSE
    edge_demand_multiplier = 1.0
END IF

projected_edge_requests = monthly_api_calls_by_region
    .filter(region IN rural_regions_improved)
    .sum() * edge_demand_multiplier * growth_projection_factor(completion_date)

-- エッジノード追加必要数
additional_edge_nodes_required = CEIL(projected_edge_requests / edge_node_capacity) - edge_deployment_count
```

### Step 3: インパクトスコア算出

| 条件 | スコア範囲 | レベル |
|------|-----------|--------|
| potential_annual_revenue_increase >= 3億円 AND additional_edge_nodes >= 10 | 60.0–80.0 | high |
| potential_annual_revenue_increase >= 1億円 | 40.0–59.9 | medium |
| potential_annual_revenue_increase >= 3000万円 | 20.0–39.9 | low |
| potential_annual_revenue_increase < 3000万円 | 5.0–19.9 | low |

```
base_score = lookup_from_table(potential_annual_revenue_increase, additional_edge_nodes_required)

-- 調整係数
IF underserved_region_leads >= 100 THEN
    -- 既に未整備地域からの問合せが多い場合、機会の確度が高い
    adjustment = +8.0
ELIF underserved_region_leads >= 30 THEN
    adjustment = +4.0
ELSE
    adjustment = +0.0
END IF

IF extracted_entities.includes_5g_guideline_change = TRUE THEN
    adjustment = adjustment + 5.0
END IF

-- 時間軸による減衰（完了まで長期の場合はスコア抑制）
years_until_completion = (completion_date - TODAY).days / 365
IF years_until_completion > 2 THEN
    time_decay = -5.0 * (years_until_completion - 2)
ELSE
    time_decay = 0.0
END IF

impact_score = CLAMP(base_score + adjustment + time_decay, 0.0, 100.0)
```

### Step 4: 戦略的位置付け判定

```
IF potential_new_customers >= 500 AND edge_demand_multiplier > 1.0 THEN
    strategic_relevance = 'growth_opportunity'
ELIF potential_new_customers >= 100 THEN
    strategic_relevance = 'incremental_expansion'
ELSE
    strategic_relevance = 'monitoring_only'
END IF
```

## 出力

### インパクト診断結果

| フィールド | 型 | 説明 |
|-----------|-----|------|
| impact_score | decimal(5,2) | インパクトスコア（0.00–100.00） |
| impact_level | string | インパクトレベル（high / medium / low） |
| strategic_relevance | string | 戦略的位置付け（growth_opportunity / incremental_expansion / monitoring_only） |
| potential_new_customers | int | 潜在新規顧客数 |
| potential_annual_revenue_increase_jpy | decimal | 潜在年間売上増加額（円） |
| additional_edge_nodes_required | int | 追加エッジノード必要数 |
| newly_covered_households | int | 新規カバー世帯数 |
| completion_timeline | string | 整備完了見込み時期 |
| edge_demand_multiplier | float | エッジ需要倍率 |

### 推奨アクション

| 優先度 | アクション | 担当 | 期限目安 |
|--------|-----------|------|----------|
| P2 | 新規整備予定地域の事業所・潜在顧客リスト作成 | マーケティングチーム | 1ヶ月以内 |
| P2 | エッジ推論ノード配置計画の見直し（地方拠点追加検討） | インフラチーム | 2ヶ月以内 |
| P3 | ローカル5G対応AIサービスの技術検証計画策定 | AI研究開発チーム | 3ヶ月以内 |
| P3 | 通信事業者との連携・パートナーシップ機会の探索 | 事業開発チーム | 3ヶ月以内 |
| P3 | 低帯域・高レイテンシ環境向けAPIの軽量化ロードマップ策定 | プロダクトチーム | 4ヶ月以内 |

### 通知設定

| 条件 | チャネル | 宛先 |
|------|---------|------|
| impact_level = 'high' | Teams (当日) | AI事業部マネージャー, 事業開発チーム |
| impact_level = 'medium' | Email (当日) | AI事業部メンバー |
| impact_level = 'low' | Email (週次) | AI事業部メンバー（参考情報） |
| strategic_relevance = 'growth_opportunity' | Teams (当日) | 経営企画部, マーケティング部長 |

## 参照データソース

### Fabric テーブル

| レイクハウス | テーブル | 用途 |
|-------------|---------|------|
| lh_ai_agent_gold | customers | 国内顧客数・地域別分布 |
| lh_ai_agent_gold | transactions | 地域別APIコール数・ARPU算出 |
| lh_ai_agent_gold | contracts | 地域別契約状況 |
| lh_common_gold | unified_customers | 統合顧客の地域属性 |
| lh_common_gold | customer_segments | 地方企業セグメント情報 |
| lh_news_gold | news_articles | ニュース記事メタデータ |
| lh_news_gold | impact_analyses | 過去のインフラ系インパクト分析（トレンド比較用） |

### 外部参照

| ソース | 用途 |
|--------|------|
| 総務省 情報通信統計データベース | 地域別ブロードバンド普及率・整備計画 |
| 総務省 ローカル5Gガイドライン | 規制緩和の詳細内容確認 |
| 国勢調査データ（e-Stat） | 未整備地域の人口・事業所数推定 |
| 自社モニタリングシステム | 地域別レイテンシ・接続品質データ |
