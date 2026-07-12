# SNS事業：通信品質基準厳格化インパクト判断スキル

## 概要

総務省による通信品質基準・冗長性要件の厳格化がSNS事業に与えるインパクトを判断するスキル。通信インフラへの高い依存度を前提に、大規模通信障害時のサービス停止リスク、CDN/マルチキャリア対応の追加コスト、ユーザーエンゲージメント損失、および広告収益への波及を評価し、インフラレジリエンス強化の優先度と推奨アクションを出力する。

## 入力パラメータ

### ニュース情報

| パラメータ | 型 | 説明 |
|---|---|---|
| `news_category` | string | ニュースカテゴリ（`infrastructure_regulation`） |
| `outage_duration_hours` | int | 契機となった通信障害の継続時間（時間） |
| `affected_subscribers_millions` | float | 影響を受けた回線数（百万） |
| `new_availability_target` | float | 新基準の稼働率目標（例: `0.999999`） |
| `new_redundancy_requirement` | string | 新冗長性要件（例: `multi_carrier`, `geo_redundant`） |
| `applies_to_ott` | bool | OTT/プラットフォーム事業者への直接適用有無 |
| `indirect_cost_pass_through` | bool | 通信事業者からのコスト転嫁の見込み |
| `enforcement_timeline_months` | int | 施行までの猶予期間（月） |

### 業務データ

| パラメータ | 型 | 説明 |
|---|---|---|
| `dau` | int | 日次アクティブユーザー数 |
| `daily_ad_revenue_jpy` | int | 日次広告収益（円） |
| `primary_carrier_dependency_ratio` | float | 主要キャリア依存比率（0.0〜1.0） |
| `cdn_provider_count` | int | 利用中CDNプロバイダー数 |
| `current_multi_carrier_enabled` | bool | マルチキャリア接続対応済みか |
| `edge_cache_locations` | int | エッジキャッシュ拠点数 |
| `monthly_infra_cost_jpy` | int | 月間インフラ費用（円） |
| `last_outage_dau_drop_ratio` | float | 直近障害時のDAU低下率（0.0〜1.0） |
| `avg_session_count_per_day` | float | ユーザーあたり日次平均セッション数 |
| `push_notification_delivery_rate` | float | プッシュ通知到達率（0.0〜1.0） |
| `offline_mode_capability` | enum | `full` / `partial` / `none` |

## 判断ロジック

### Step 1: サービス停止リスクの評価

```
-- 通信障害発生時のSNSサービス影響度
IF primary_carrier_dependency_ratio >= 0.7 AND NOT current_multi_carrier_enabled THEN
    outage_vulnerability = "critical"
ELSE IF primary_carrier_dependency_ratio >= 0.5 OR cdn_provider_count <= 1 THEN
    outage_vulnerability = "high"
ELSE IF primary_carrier_dependency_ratio >= 0.3 THEN
    outage_vulnerability = "medium"
ELSE
    outage_vulnerability = "low"

vulnerability_weight:
  critical = 1.0
  high = 0.7
  medium = 0.4
  low = 0.15
```

### Step 2: 障害時の収益損失推定

```
-- 過去実績ベースの損失推定
estimated_outage_hours = outage_duration_hours × primary_carrier_dependency_ratio
dau_loss_during_outage = dau × last_outage_dau_drop_ratio

-- 広告収益損失（障害時間あたり）
hourly_ad_revenue_jpy = daily_ad_revenue_jpy / 24
outage_revenue_loss_jpy = hourly_ad_revenue_jpy × estimated_outage_hours × last_outage_dau_drop_ratio

-- 障害後のエンゲージメント回復遅延（通常48時間で完全回復）
recovery_period_hours = 48
recovery_revenue_loss_jpy = hourly_ad_revenue_jpy × recovery_period_hours × (last_outage_dau_drop_ratio × 0.3)

total_single_outage_loss_jpy = outage_revenue_loss_jpy + recovery_revenue_loss_jpy
```

### Step 3: インフラ強化コストの算出

```
-- マルチキャリア対応コスト
IF NOT current_multi_carrier_enabled THEN
    multi_carrier_cost_jpy = 800_000_000  -- 初期構築8億円
    multi_carrier_monthly_jpy = 50_000_000  -- 月額追加5000万円
ELSE
    multi_carrier_cost_jpy = 0
    multi_carrier_monthly_jpy = 0

-- CDN冗長化コスト
target_cdn_count = 3  -- 最低3プロバイダー推奨
additional_cdn_count = MAX(0, target_cdn_count - cdn_provider_count)
cdn_expansion_cost_jpy = additional_cdn_count × 300_000_000  -- プロバイダー追加あたり3億円

-- エッジキャッシュ拠点拡大コスト
target_edge_locations = 16  -- 国内16拠点推奨
additional_edge_locations = MAX(0, target_edge_locations - edge_cache_locations)
edge_expansion_cost_jpy = additional_edge_locations × 150_000_000  -- 拠点あたり1.5億円

-- 通信事業者からのコスト転嫁
IF indirect_cost_pass_through THEN
    pass_through_monthly_increase_jpy = monthly_infra_cost_jpy × 0.12  -- 12%増
ELSE
    pass_through_monthly_increase_jpy = 0

total_infra_upgrade_capex_jpy = multi_carrier_cost_jpy + cdn_expansion_cost_jpy + edge_expansion_cost_jpy
annual_infra_opex_increase_jpy = (multi_carrier_monthly_jpy + pass_through_monthly_increase_jpy) × 12
```

### Step 4: ユーザー体験・エンゲージメント影響

```
-- オフライン対応レベルによる緩和
offline_mitigation:
  full = 0.7      -- オフライン時も70%の機能利用可能
  partial = 0.3
  none = 0.0

-- 通知到達率低下による再訪問率への影響
IF push_notification_delivery_rate < 0.8 THEN notification_risk = "high"
ELSE IF push_notification_delivery_rate < 0.9 THEN notification_risk = "medium"
ELSE notification_risk = "low"

-- 障害頻度増加（基準厳格化前の過渡期）によるDAU影響
-- 年間想定障害回数 × 回あたりDAU低下
annual_dau_impact = (2 × last_outage_dau_drop_ratio × dau) × (1.0 - offline_mitigation)
annual_engagement_loss_sessions = annual_dau_impact × avg_session_count_per_day × 3  -- 3日間影響
```

### Step 5: 規制対応の直接義務評価

```
-- OTTへの直接適用がある場合の追加対応
IF applies_to_ott THEN
    -- 障害時ユーザー通知義務
    notification_system_cost_jpy = 500_000_000
    -- 定期報告義務
    reporting_cost_annual_jpy = 150_000_000
    -- BCP策定・訓練義務
    bcp_cost_annual_jpy = 100_000_000
    direct_regulatory_cost_jpy = notification_system_cost_jpy + reporting_cost_annual_jpy + bcp_cost_annual_jpy
ELSE
    direct_regulatory_cost_jpy = 0
```

### Step 6: 総合インパクトスコア算出

```
-- 財務影響の正規化（年間インフラコストの50%を閾値とする）
annual_infra_baseline = monthly_infra_cost_jpy × 12
total_additional_annual_cost = annual_infra_opex_increase_jpy + direct_regulatory_cost_jpy
financial_pressure = MIN(1.0, (total_infra_upgrade_capex_jpy / 3 + total_additional_annual_cost) / (annual_infra_baseline × 0.5))

-- 収益リスクの正規化
annual_ad_revenue = daily_ad_revenue_jpy × 365
revenue_risk_ratio = MIN(1.0, (total_single_outage_loss_jpy × 4) / (annual_ad_revenue × 0.05))
-- 年4回の障害想定、年間広告収益の5%を閾値

raw_score = (vulnerability_weight × 0.30 + financial_pressure × 0.30 + revenue_risk_ratio × 0.20 + (1.0 - offline_mitigation) × 0.20) × 100
impact_score = clamp(raw_score, 0, 100)
```

### Step 7: インパクトレベル判定

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
| `outage_vulnerability` | enum | サービス停止脆弱度 |
| `single_outage_loss_jpy` | int | 1回あたり障害損失推定額（円） |
| `infra_upgrade_capex_jpy` | int | インフラ強化CAPEX推定額（円） |
| `annual_opex_increase_jpy` | int | 年間OPEX増加推定額（円） |
| `offline_readiness` | enum | オフライン対応レベル |
| `recommended_actions` | list[string] | 推奨アクションリスト |
| `risk_summary` | string | リスクサマリー文（自然言語） |

### 推奨アクション分岐

| レベル | 推奨アクション |
|---|---|
| critical | マルチキャリア接続の緊急導入（単一障害点の即時排除）、CDN3社以上の冗長構成への移行、エッジキャッシュ16拠点への拡大プロジェクト立ち上げ、オフラインモード機能の緊急実装（ローカルキャッシュ活用）、障害時代替通知手段の確保（SMS/メール多重化）、インフラBCP策定と四半期訓練の即時開始 |
| high | CDNプロバイダーの追加契約、エッジキャッシュの段階的増設、プッシュ通知の多経路化（FCM+独自経路）、障害時のグレースフルデグラデーション設計、通信コスト増加分の予算確保 |
| medium | インフラ依存度の詳細アセスメント実施、オフラインキャッシュ機能の技術検証、障害シミュレーション（カオスエンジニアリング）の定期実施、通信事業者との SLA 再交渉 |
| low | インフラ冗長性のモニタリング継続、次期アーキテクチャ計画への反映、業界動向のウォッチ |

## 参照データソース（Fabric テーブル）

| テーブル名 | 説明 | 主要カラム |
|---|---|---|
| `fact_service_availability` | サービス稼働率実績 | `date`, `hour`, `availability_rate`, `error_count`, `affected_users`, `region` |
| `fact_infra_incidents` | インフラ障害履歴 | `incident_id`, `start_time`, `end_time`, `root_cause`, `carrier_name`, `affected_dau`, `revenue_loss_jpy` |
| `fact_dau_hourly` | 時間別DAU実績 | `datetime`, `dau`, `session_count`, `avg_session_duration`, `carrier_breakdown` |
| `dim_infra_topology` | インフラ構成マスタ | `component_id`, `type`, `provider`, `region`, `redundancy_level`, `capacity` |
| `fact_cdn_performance` | CDNパフォーマンス実績 | `date`, `cdn_provider`, `cache_hit_ratio`, `latency_p95_ms`, `availability`, `traffic_gb` |
| `fact_push_notification_delivery` | プッシュ通知配信実績 | `date`, `carrier`, `sent_count`, `delivered_count`, `delivery_rate`, `avg_latency_ms` |
| `fact_infra_costs` | インフラ費用実績 | `month`, `cost_category`, `provider`, `amount_jpy`, `usage_volume` |
| `agg_outage_impact_summary` | 障害影響サマリー集計 | `incident_id`, `total_downtime_minutes`, `dau_drop_ratio`, `revenue_loss_jpy`, `recovery_hours`, `user_complaints` |
