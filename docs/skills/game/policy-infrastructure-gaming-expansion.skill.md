# スキル: 通信インフラ拡充によるオンラインゲーム市場拡大機会分析

## 概要

国内通信インフラ整備（光ファイバー、5G等）に関する政策ニュースを分析し、当社ゲーム事業のオンラインゲーム・クラウドゲーミングサービスにおける潜在ユーザー拡大、地方部のプレイ体験改善、サーバー配置最適化への影響を判断するスキル。新規整備地域の人口特性・ゲーム市場浸透率から、中長期的な事業機会とインフラ対応計画を算出する。

## 入力パラメータ

### ニュース情報

| パラメータ | 型 | 説明 |
|-----------|-----|------|
| article_id | string | ニュース記事ID |
| title | string | 記事タイトル |
| summary | string | 記事要約 |
| category | string | カテゴリ（policy_infra） |
| published_at | datetime | 配信日時 |
| extracted_entities | object[] | 抽出エンティティ（予算額、対象世帯数、カバー率、整備完了日、5G関連情報等） |

### 業務データ

| パラメータ | 型 | 説明 | ソース |
|-----------|-----|------|--------|
| total_mau_japan | int | 国内MAU | sqldb_game_01.customers (country_code='JP') |
| mau_by_region | object[] | 地域別MAU分布 | sqldb_game_01.customers GROUP BY region |
| revenue_by_region | object[] | 地域別月間売上 | sqldb_game_01.transactions GROUP BY region |
| online_title_count | int | オンライン対戦/協力機能を持つタイトル数 | sqldb_game_01.game_titles |
| cloud_gaming_subscribers | int | クラウドゲーミングサブスク会員数 | sqldb_game_01.customers (service='cloud_gaming') |
| avg_session_bandwidth_mbps | float | 平均セッション帯域幅（Mbps） | テレメトリデータ |
| latency_complaint_rate | float | レイテンシ関連苦情率（%） | サポートデータ |
| rural_user_churn_rate | float | 地方部ユーザーの解約率（%） | sqldb_game_01.customers + transactions |
| server_locations_japan | object[] | 国内サーバー配置拠点 | インフラ台帳 |
| arpu_japan | decimal | 国内ARPU（円） | sqldb_game_01.transactions (集計) |

## 判断ロジック

### Step 1: 潜在ユーザー規模の算出

```
newly_covered_households = extracted_entities.target_households  -- 520,000世帯
target_completion_date = extracted_entities.completion_date  -- 2028年度末

-- 新規カバー世帯からのゲームユーザー推定
-- 世帯あたり平均人口 × ゲーム人口比率 × オンラインゲーム比率
avg_persons_per_household = 2.3
gaming_population_ratio = 0.45  -- 日本のゲーム人口比率
online_gaming_ratio = 0.65  -- ゲーム人口中のオンラインゲーム利用率

potential_new_gamers = newly_covered_households * avg_persons_per_household
                    * gaming_population_ratio * online_gaming_ratio
-- 520,000 * 2.3 * 0.45 * 0.65 ≈ 349,830人

-- 当社サービス獲得見込み（市場シェアベース）
market_share = total_mau_japan / total_japan_online_gamers
potential_new_users = potential_new_gamers * market_share
```

### Step 2: 収益インパクト推定

```
-- 新規ユーザーからの年間売上見込み
potential_annual_revenue = potential_new_users * arpu_japan * 12

-- 既存地方ユーザーの離脱率改善効果
rural_users_current = mau_by_region.filter(type='rural').sum()
churn_improvement_rate = 0.15  -- インフラ改善による離脱率15%改善想定
retained_users = rural_users_current * rural_user_churn_rate * churn_improvement_rate
retention_revenue_impact = retained_users * arpu_japan * 12

-- クラウドゲーミング拡大機会
IF extracted_entities.includes_5g_guideline_change = TRUE THEN
    cloud_gaming_expansion_factor = 1.4  -- 5G普及でクラウドゲーミング需要40%増
ELSE
    cloud_gaming_expansion_factor = 1.15
END IF
cloud_gaming_potential_revenue = cloud_gaming_subscribers * cloud_gaming_expansion_factor * arpu_japan * 12

total_revenue_opportunity = potential_annual_revenue + retention_revenue_impact + cloud_gaming_potential_revenue
```

### Step 3: インパクトスコア算出

| 条件 | スコア範囲 | レベル |
|------|-----------|--------|
| total_revenue_opportunity >= 5億円 AND potential_new_users >= 50,000 | 55.0–75.0 | medium-high |
| total_revenue_opportunity >= 2億円 AND potential_new_users >= 20,000 | 35.0–54.9 | medium |
| total_revenue_opportunity >= 5000万円 | 20.0–34.9 | low |
| total_revenue_opportunity < 5000万円 | 5.0–19.9 | low |

```
base_score = lookup_from_table(total_revenue_opportunity, potential_new_users)

-- 調整係数
IF latency_complaint_rate >= 5.0 THEN
    -- レイテンシ苦情が多い場合、インフラ改善の恩恵大
    adjustment = +8.0
ELIF latency_complaint_rate >= 2.0 THEN
    adjustment = +4.0
ELSE
    adjustment = +0.0
END IF

IF extracted_entities.includes_5g_guideline_change = TRUE THEN
    -- 5Gガイドライン改定はクラウドゲーミングに直接影響
    adjustment = adjustment + 6.0
END IF

IF online_title_count >= 10 THEN
    -- オンラインタイトルが多い場合、恩恵を受けるタイトル数が多い
    adjustment = adjustment + 4.0
END IF

-- 時間軸による減衰
years_until_completion = (target_completion_date - TODAY).days / 365
IF years_until_completion > 2 THEN
    time_decay = -4.0 * (years_until_completion - 2)
ELSE
    time_decay = 0.0
END IF

impact_score = CLAMP(base_score + adjustment + time_decay, 0.0, 100.0)
```

### Step 4: サーバー配置最適化判定

```
-- 新規整備地域とサーバー拠点の距離分析
underserved_regions = extract_regions(extracted_entities.target_areas)

FOR EACH region IN underserved_regions:
    nearest_server = find_nearest(server_locations_japan, region)
    estimated_latency = calculate_latency(nearest_server, region)

    IF estimated_latency > 30 THEN  -- 30ms以上はオンラインゲームに影響
        region.needs_edge_server = TRUE
    ELSE
        region.needs_edge_server = FALSE
    END IF
END FOR

regions_needing_servers = count(regions WHERE needs_edge_server = TRUE)

IF regions_needing_servers >= 3 THEN
    server_action = 'expansion_recommended'
ELIF regions_needing_servers >= 1 THEN
    server_action = 'evaluation_needed'
ELSE
    server_action = 'current_sufficient'
END IF
```

## 出力

### インパクト診断結果

| フィールド | 型 | 説明 |
|-----------|-----|------|
| impact_score | decimal(5,2) | インパクトスコア（0.00–100.00） |
| impact_level | string | インパクトレベル（high / medium / low） |
| potential_new_users | int | 潜在新規ユーザー数 |
| potential_annual_revenue_jpy | decimal | 潜在年間売上機会（円） |
| retention_revenue_impact_jpy | decimal | 離脱率改善による年間売上効果（円） |
| cloud_gaming_expansion_factor | float | クラウドゲーミング拡大倍率 |
| total_revenue_opportunity_jpy | decimal | 総収益機会（円） |
| regions_needing_servers | int | エッジサーバー追加検討地域数 |
| server_action | string | サーバー対応方針 |
| completion_timeline | string | インフラ整備完了見込み時期 |

### 推奨アクション

| 優先度 | アクション | 担当 | 期限目安 |
|--------|-----------|------|----------|
| P3 | 中山間地域・離島部からのアクセスログ分析・潜在ユーザー数推計 | データ分析チーム | 1ヶ月以内 |
| P3 | 低帯域環境向けネットコード最適化の継続検討 | ネットワークエンジニア | 2ヶ月以内 |
| P3 | クラウドゲーミングサービスの5G対応品質検証計画策定 | クラウドゲーミングチーム | 3ヶ月以内 |
| P3 | 地方部サーバー配置の費用対効果分析（新規整備地域ベース） | インフラチーム | 3ヶ月以内 |
| P4 | 地方部向けマーケティング施策の中長期計画策定 | マーケティング部 | 6ヶ月以内 |

### 通知設定

| 条件 | チャネル | 宛先 |
|------|---------|------|
| impact_level = 'medium-high' | Teams (当日) | 事業企画部, クラウドゲーミングチームリード |
| impact_level = 'medium' | Email (当日) | ゲーム事業部マネージャー |
| impact_level = 'low' | Email (週次) | ゲーム事業部メンバー（参考情報） |
| server_action = 'expansion_recommended' | Teams (当日) | インフラチームリード |

## 参照データソース

### Fabric テーブル

| レイクハウス | テーブル | 用途 |
|-------------|---------|------|
| lh_game_gold | game_titles | オンライン対応タイトル一覧・帯域要件 |
| lh_game_gold | customers | 地域別MAU・プラットフォーム分布・解約率 |
| lh_game_gold | transactions | 地域別売上・ARPU算出 |
| lh_game_gold | inventory | クラウドゲーミング対応コンテンツ一覧 |
| lh_common_gold | unified_customers | 統合顧客の地域属性 |
| lh_common_gold | customer_segments | 地方ユーザーセグメント |
| lh_news_gold | news_articles | ニュース記事メタデータ |
| lh_news_gold | impact_analyses | 過去のインフラ系インパクト分析 |

### 外部参照

| ソース | 用途 |
|--------|------|
| 総務省 情報通信統計データベース | 地域別ブロードバンド普及率・世帯数 |
| CESA ゲーム白書 | 国内ゲーム市場規模・オンラインゲーム比率 |
| 国勢調査データ（e-Stat） | 未整備地域の人口・年齢構成 |
| 自社テレメトリシステム | プレイヤーの接続品質・レイテンシデータ |
