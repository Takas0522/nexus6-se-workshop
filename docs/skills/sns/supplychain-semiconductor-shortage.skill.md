# SNS事業：半導体供給不足インパクト判断スキル

## 概要

世界的な半導体供給不足がSNS事業に与える間接的インパクトを判断するスキル。端末出荷遅延に伴う新規ユーザー獲得の鈍化、既存ユーザーのデバイス老朽化によるアプリ体験劣化、および広告表示機会の減少を総合的に評価し、推奨アクションを出力する。

## 入力パラメータ

### ニュース情報

| パラメータ | 型 | 説明 |
|---|---|---|
| `news_category` | string | ニュースカテゴリ（`supply_chain`） |
| `affected_components` | list[string] | 影響を受ける半導体コンポーネント（例: `SoC`, `memory`, `display_driver`） |
| `supply_shortage_severity` | enum | `critical` / `severe` / `moderate` / `minor` |
| `estimated_delay_weeks` | int | 推定端末出荷遅延期間（週） |
| `affected_manufacturers` | list[string] | 影響を受ける端末メーカー |
| `geographic_scope` | enum | `global` / `regional_asia` / `regional_other` |

### 業務データ

| パラメータ | 型 | 説明 |
|---|---|---|
| `monthly_new_user_signups` | int | 月間新規ユーザー登録数 |
| `new_device_signup_ratio` | float | 新端末購入時のサインアップ比率（0.0〜1.0） |
| `dau` | int | 日次アクティブユーザー数 |
| `mau` | int | 月次アクティブユーザー数 |
| `avg_session_time_minutes` | float | 平均セッション時間（分） |
| `old_device_user_ratio` | float | 3年以上前の端末を使用するユーザー比率（0.0〜1.0） |
| `ad_revenue_per_dau_jpy` | float | DAUあたり日次広告収益（円） |
| `app_crash_rate_old_devices` | float | 旧端末でのアプリクラッシュ率 |
| `monthly_ad_impressions` | int | 月間広告インプレッション数 |
| `cpm_avg_jpy` | float | 平均CPM（千インプレッション単価、円） |

## 判断ロジック

### Step 1: ユーザー獲得への影響評価

```
-- 端末出荷遅延による新規サインアップ減少の推定
IF geographic_scope == "global" THEN market_impact_ratio = 0.8
ELSE IF geographic_scope == "regional_asia" THEN market_impact_ratio = 0.6
ELSE market_impact_ratio = 0.3

severity_factor:
  critical = 0.7   -- 新端末出荷70%減
  severe = 0.5
  moderate = 0.3
  minor = 0.1

new_signup_reduction = monthly_new_user_signups × new_device_signup_ratio × severity_factor × market_impact_ratio
signup_impact_months = estimated_delay_weeks / 4
total_lost_signups = new_signup_reduction × signup_impact_months
```

### Step 2: 既存ユーザー体験劣化の評価

```
-- 端末買い替えが遅延することで旧端末ユーザーが増加
device_aging_acceleration = old_device_user_ratio + (severity_factor × 0.15 × (estimated_delay_weeks / 12))
device_aging_acceleration = MIN(device_aging_acceleration, 0.95)

-- 旧端末でのUX劣化による離脱リスク
IF app_crash_rate_old_devices >= 0.05 THEN ux_risk = "high"       -- 5%以上のクラッシュ率
ELSE IF app_crash_rate_old_devices >= 0.02 THEN ux_risk = "medium"
ELSE ux_risk = "low"

-- エンゲージメント低下の推定
engagement_decline_ratio:
  ux_risk "high" = 0.15     -- セッション時間15%低下
  ux_risk "medium" = 0.08
  ux_risk "low" = 0.03

estimated_session_decline_minutes = avg_session_time_minutes × engagement_decline_ratio
```

### Step 3: 広告収益への影響算出

```
-- DAU減少の推定（新規獲得減 + UX劣化による離脱）
monthly_churn_from_ux = dau × old_device_user_ratio × (app_crash_rate_old_devices × 0.5)
dau_decline = (new_signup_reduction / 30) + monthly_churn_from_ux

-- 広告インプレッション減少
impression_decline_ratio = (dau_decline / dau) + (engagement_decline_ratio × 0.7)
lost_impressions_monthly = monthly_ad_impressions × impression_decline_ratio

-- 収益影響額
monthly_ad_revenue_loss_jpy = (lost_impressions_monthly / 1000) × cpm_avg_jpy
total_revenue_loss_jpy = monthly_ad_revenue_loss_jpy × signup_impact_months
```

### Step 4: プラットフォーム技術負債の評価

```
-- 旧端末対応の技術コスト増大
IF device_aging_acceleration >= 0.5 THEN tech_debt_pressure = "high"
ELSE IF device_aging_acceleration >= 0.3 THEN tech_debt_pressure = "medium"
ELSE tech_debt_pressure = "low"

tech_debt_weight:
  high = 0.25    -- 新機能開発リソースの25%が旧端末対応に消費
  medium = 0.15
  low = 0.05
```

### Step 5: 総合インパクトスコア算出

```
-- 収益影響の正規化（月間広告収益に対する損失比率）
monthly_total_ad_revenue = (monthly_ad_impressions / 1000) × cpm_avg_jpy
revenue_impact_ratio = MIN(1.0, monthly_ad_revenue_loss_jpy / (monthly_total_ad_revenue × 0.2))

-- ユーザー成長影響の正規化
growth_impact_ratio = MIN(1.0, total_lost_signups / (monthly_new_user_signups × 3))

-- UXリスクウェイト
ux_weight:
  high = 0.8
  medium = 0.4
  low = 0.15

raw_score = (revenue_impact_ratio × 0.35 + growth_impact_ratio × 0.30 + ux_weight × 0.20 + tech_debt_weight × 0.15) × 100
impact_score = clamp(raw_score, 0, 100)
```

### Step 6: インパクトレベル判定

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
| `estimated_lost_signups` | int | 新規ユーザー獲得損失推定数 |
| `estimated_dau_decline` | int | DAU減少推定数 |
| `monthly_ad_revenue_loss_jpy` | int | 月間広告収益損失推定額（円） |
| `ux_risk_level` | enum | UX劣化リスクレベル |
| `tech_debt_pressure` | enum | 技術負債圧力レベル |
| `recommended_actions` | list[string] | 推奨アクションリスト |
| `risk_summary` | string | リスクサマリー文（自然言語） |

### 推奨アクション分岐

| レベル | 推奨アクション |
|---|---|
| critical | ライトウェイト版アプリ（Lite）の緊急開発・リリース、旧端末向けパフォーマンス最適化スプリントの実施、Web版/PWAへのトラフィック誘導強化、広告主への影響通知と単価維持交渉、ユーザーリテンション施策の緊急発動 |
| high | アプリの最低動作要件の据え置き方針決定、旧端末向けUI簡素化モードの実装、新規ユーザー獲得チャネルの多様化（端末非依存チャネル強化）、エンゲージメント低下ユーザーへのプッシュ通知最適化 |
| medium | 端末別パフォーマンスモニタリングの強化、次期アプリアップデートでの旧端末互換性テスト拡充、デバイス分布レポートの頻度引き上げ |
| low | デバイス動向のモニタリング継続、四半期レビューでの報告、長期的なPWA戦略の検討 |

## 参照データソース（Fabric テーブル）

| テーブル名 | 説明 | 主要カラム |
|---|---|---|
| `fact_user_signups` | ユーザー新規登録イベント | `signup_date`, `user_id`, `device_model`, `os_version`, `acquisition_channel` |
| `fact_daily_active_users` | DAU実績 | `date`, `user_id`, `session_count`, `session_duration_minutes`, `device_model` |
| `fact_ad_impressions` | 広告インプレッション実績 | `date`, `ad_slot_id`, `impressions`, `clicks`, `revenue_jpy`, `device_tier` |
| `fact_app_performance` | アプリパフォーマンス指標 | `date`, `device_model`, `os_version`, `crash_rate`, `anr_rate`, `avg_load_time_ms` |
| `dim_user_devices` | ユーザー端末情報マスタ | `user_id`, `device_model`, `os_version`, `device_release_year`, `last_seen_date` |
| `fact_user_churn` | ユーザー離脱イベント | `churn_date`, `user_id`, `last_device_model`, `days_since_last_active`, `churn_reason` |
| `agg_monthly_growth` | 月次ユーザー成長集計 | `month`, `new_signups`, `churned_users`, `net_growth`, `dau_avg`, `mau` |
| `agg_device_distribution` | 端末分布集計 | `snapshot_date`, `device_model`, `device_age_years`, `user_count`, `avg_session_time`, `crash_rate` |
