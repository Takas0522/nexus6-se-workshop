# SNS事業：半導体供給制約インパクト判断スキル

## 前提

- 本文書は nexus6-se-workshop の Demo 用ナレッジであり、数値・事例はすべて想定値である。
- 個人情報、実在企業の機密、実顧客の行動履歴は含めない。
- 対象事業部は SNS事業 である。
- 対応シナリオは「世界的半導体供給制約の深刻化（サプライチェーンリスク）」。
- Agent 2 は影響有無の評価、Agent 3 は事業部別 Next Action の草案作成に使う。

## 概要

米中間の半導体輸出規制強化に伴うサーバー・GPU機器の調達遅延が、SNSプラットフォームのインフラ拡張計画、広告配信エンジンの処理能力、AI/MLベースのレコメンデーション・コンテンツモデレーション機能に与えるインパクトを判断するスキル。サーバー増設計画への影響、広告配信品質の低下リスク、ユーザー体験劣化によるDAU減少可能性を定量評価し、推奨アクションを出力する。

## 適用シナリオ

- 外部ニュースのトリガー: 半導体輸出規制の報道、チップメーカーの出荷遅延発表、サーバーベンダーの納期延長通知、GPU調達に関する市場レポート。
- SNS事業は携帯端末を直接販売しないが、プラットフォーム運用のためのサーバー・GPU・ネットワーク機器の安定調達がサービス品質に直結する。
- 広告配信エンジンはGPUクラスタに依存しており、計画増設の遅延は広告収益に影響する。

## 入力パラメータ

### ニュース情報

| パラメータ | 型 | 説明 |
|---|---|---|
| `news_category` | string | ニュースカテゴリ（`supply_chain`） |
| `affected_chip_types` | list[string] | 影響を受ける半導体種別（例: `server_cpu`, `gpu`, `memory`, `network_asic`） |
| `supply_delay_months` | int | 供給遅延期間（月数、例: 3〜6） |
| `affected_manufacturers` | list[string] | 影響を受けるメーカー名 |
| `regulation_scope` | string | 規制範囲（例: `export_ban`, `license_required`, `entity_list`） |
| `price_increase_pct` | float | 調達コスト上昇率（%） |
| `secondary_source_available` | bool | 代替調達先の有無 |

### 業務データ

| パラメータ | 型 | 説明 | 参照テーブル |
|---|---|---|---|
| `current_server_capacity_pct` | float | 現在のサーバー利用率（%） | インフラ監視 |
| `planned_expansion_servers` | int | 計画中の増設サーバー台数 | インフラ計画書 |
| `expansion_target_date` | date | 増設完了目標日 | インフラ計画書 |
| `gpu_cluster_utilization_pct` | float | GPU クラスタ利用率（%） | インフラ監視 |
| `gpu_expansion_planned` | int | GPU増設計画台数 | インフラ計画書 |
| `ad_delivery_latency_ms` | float | 広告配信平均レイテンシ（ms） | 広告配信システム |
| `ad_delivery_sla_ms` | float | 広告配信SLA上限（ms） | SLA定義 |
| `monthly_ad_revenue_jpy` | int | 月間広告収益（円） | `sns_transactions` 集計 |
| `dau` | int | DAU（日次アクティブユーザー数） | `sns_users` 集計 |
| `active_ad_campaigns` | int | 稼働中の広告キャンペーン数 | `sns_ad_campaigns` (status=ACTIVE) |
| `ml_model_retraining_gpu_hours` | float | ML モデル再学習に必要なGPU時間/月 | ML基盤管理 |
| `content_moderation_queue_hours` | float | コンテンツモデレーション処理待ち時間 | モデレーションシステム |
| `vendor_contracts_expiring_90d` | int | 90日以内に期限到来するベンダー契約数 | 調達管理 |

## 判断ロジック

### Step 1: インフラ拡張への影響度評価

```
-- サーバー増設遅延リスク
IF "server_cpu" IN affected_chip_types OR "memory" IN affected_chip_types THEN
  server_expansion_at_risk = TRUE
  server_delay_months = supply_delay_months
ELSE
  server_expansion_at_risk = FALSE
  server_delay_months = 0

-- GPU増設遅延リスク
IF "gpu" IN affected_chip_types THEN
  gpu_expansion_at_risk = TRUE
  gpu_delay_months = supply_delay_months
ELSE
  gpu_expansion_at_risk = FALSE
  gpu_delay_months = 0

-- キャパシティ逼迫判定
months_to_capacity_limit:
  IF current_server_capacity_pct >= 85 THEN 2
  ELSE IF current_server_capacity_pct >= 75 THEN 4
  ELSE IF current_server_capacity_pct >= 65 THEN 6
  ELSE 12

IF server_expansion_at_risk AND server_delay_months > months_to_capacity_limit THEN
  capacity_risk = "critical"
ELSE IF server_expansion_at_risk AND server_delay_months >= months_to_capacity_limit THEN
  capacity_risk = "high"
ELSE IF server_expansion_at_risk THEN
  capacity_risk = "medium"
ELSE
  capacity_risk = "low"
```

### Step 2: 広告配信品質への影響評価

```
-- 現在のSLAマージン
latency_margin = ad_delivery_sla_ms - ad_delivery_latency_ms
latency_margin_pct = latency_margin / ad_delivery_sla_ms × 100

-- GPU不足による広告配信劣化予測
IF gpu_expansion_at_risk AND gpu_cluster_utilization_pct >= 80 THEN
  ad_quality_degradation = "high"
  estimated_revenue_loss_pct = 15.0 + (gpu_cluster_utilization_pct - 80) × 1.0
ELSE IF gpu_expansion_at_risk AND gpu_cluster_utilization_pct >= 65 THEN
  ad_quality_degradation = "medium"
  estimated_revenue_loss_pct = 5.0 + (gpu_cluster_utilization_pct - 65) × 0.5
ELSE IF gpu_expansion_at_risk THEN
  ad_quality_degradation = "low"
  estimated_revenue_loss_pct = 2.0
ELSE
  ad_quality_degradation = "none"
  estimated_revenue_loss_pct = 0.0

estimated_monthly_revenue_loss = monthly_ad_revenue_jpy × (estimated_revenue_loss_pct / 100)
```

### Step 3: AI/ML機能への影響評価

```
-- コンテンツモデレーション遅延リスク
IF gpu_expansion_at_risk AND content_moderation_queue_hours > 4 THEN
  moderation_risk = "critical"
ELSE IF gpu_expansion_at_risk AND content_moderation_queue_hours > 2 THEN
  moderation_risk = "high"
ELSE IF gpu_expansion_at_risk THEN
  moderation_risk = "medium"
ELSE
  moderation_risk = "low"

-- MLモデル再学習への影響
IF gpu_expansion_at_risk AND gpu_cluster_utilization_pct >= 80 THEN
  ml_retraining_impact = "delayed"
  retraining_delay_weeks = supply_delay_months × 2
ELSE IF gpu_expansion_at_risk THEN
  ml_retraining_impact = "constrained"
  retraining_delay_weeks = supply_delay_months
ELSE
  ml_retraining_impact = "none"
  retraining_delay_weeks = 0
```

### Step 4: コスト増加の評価

```
-- 調達コスト上昇の影響
IF price_increase_pct >= 30 THEN cost_impact = "severe"
ELSE IF price_increase_pct >= 15 THEN cost_impact = "significant"
ELSE IF price_increase_pct >= 5 THEN cost_impact = "moderate"
ELSE cost_impact = "minimal"

-- 代替調達先の有無による補正
IF secondary_source_available THEN
  effective_price_increase = price_increase_pct × 0.6
ELSE
  effective_price_increase = price_increase_pct

-- スポット調達の必要性
IF capacity_risk IN ("critical", "high") AND NOT secondary_source_available THEN
  spot_procurement_needed = TRUE
  spot_premium_pct = price_increase_pct × 1.5
ELSE
  spot_procurement_needed = FALSE
  spot_premium_pct = 0.0
```

### Step 5: ユーザー体験・DAU影響の推定

```
-- サービスパフォーマンス劣化による DAU 影響
IF capacity_risk == "critical" AND ad_quality_degradation IN ("high", "medium") THEN
  estimated_dau_decline_pct = 5.0
ELSE IF capacity_risk == "high" OR ad_quality_degradation == "high" THEN
  estimated_dau_decline_pct = 2.5
ELSE IF capacity_risk == "medium" OR ad_quality_degradation == "medium" THEN
  estimated_dau_decline_pct = 1.0
ELSE
  estimated_dau_decline_pct = 0.0

estimated_dau_loss = dau × (estimated_dau_decline_pct / 100)

-- モデレーション遅延によるプラットフォーム品質リスク
IF moderation_risk == "critical" THEN
  platform_quality_risk = "有害コンテンツの滞留増加。レギュレーション違反リスクあり"
ELSE IF moderation_risk == "high" THEN
  platform_quality_risk = "モデレーション遅延により報告対応が長期化"
ELSE
  platform_quality_risk = "影響軽微"
```

### Step 6: 総合インパクトスコア算出

```
capacity_risk_weight:
  critical → 1.0
  high     → 0.7
  medium   → 0.4
  low      → 0.1

ad_quality_weight:
  high → 1.0
  medium → 0.6
  low → 0.3
  none → 0.0

moderation_risk_weight:
  critical → 1.0
  high     → 0.7
  medium   → 0.3
  low      → 0.1

cost_impact_weight:
  severe      → 1.0
  significant → 0.7
  moderate    → 0.4
  minimal     → 0.1

raw_score = (capacity_risk_weight × 0.30
           + ad_quality_weight × 0.25
           + moderation_risk_weight × 0.20
           + cost_impact_weight × 0.15
           + MIN(1.0, estimated_dau_decline_pct / 5.0) × 0.10)
           × 100

impact_score = CLAMP(raw_score, 0, 100)
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
| `capacity_risk` | enum | サーバーキャパシティリスクレベル |
| `ad_quality_degradation` | enum | 広告配信品質劣化レベル |
| `moderation_risk` | enum | コンテンツモデレーションリスクレベル |
| `estimated_monthly_revenue_loss_jpy` | int | 広告収益月間損失推定額（円） |
| `estimated_dau_loss` | int | DAU減少推定数 |
| `ml_retraining_delay_weeks` | int | MLモデル再学習遅延（週） |
| `spot_procurement_needed` | bool | スポット調達の必要性 |
| `recommended_actions` | list[string] | 推奨アクションリスト |
| `notification_channel` | string | 推奨通知チャネル |
| `risk_summary` | string | リスクサマリー文（自然言語） |

### 推奨アクション分岐

| レベル | 推奨アクション | 通知チャネル |
|---|---|---|
| critical | 既存ワークロードの最適化による即時キャパシティ確保、クラウドバースト（パブリッククラウドへのオーバーフロー）の緊急設定、GPU利用の優先度再割当て（広告配信＞モデレーション＞モデル再学習）、代替半導体ベンダーとの緊急交渉開始、広告主への配信品質低下事前通知の準備、コンテンツモデレーションのルールベース代替手段導入 | slack + email |
| high | サーバー増設計画のリスケジュール策定、GPU利用効率化（モデル軽量化・推論最適化）の実施、代替調達先の選定・見積取得、広告キャンペーンの配信ペーシング調整検討、中古・リファービッシュ機器の調達可能性調査 | slack |
| medium | インフラ利用率の詳細モニタリング強化、既存リソースの最適化余地の洗い出し、ベンダーへの納期確認・情報収集、次四半期増設計画の前倒し検討 | slack |
| low | 半導体市場動向のウォッチ継続、長期調達契約の見直しポイント整理、次年度インフラ予算への反映検討 | 定期レポート |

### 通知テンプレート（critical / high 時）

```
件名: 【{impact_level}】半導体供給制約によるSNSインフラ拡張遅延リスク
本文:
半導体供給制約に伴い、SNSプラットフォームのインフラ拡張計画
および広告配信品質に影響が生じる可能性があります。
- インパクトスコア: {impact_score}/100
- サーバーキャパシティリスク: {capacity_risk}
  （現在利用率: {current_server_capacity_pct:.1f}%）
- 広告配信品質劣化: {ad_quality_degradation}
- 広告収益月間損失推定: ¥{estimated_monthly_revenue_loss_jpy:,}
- GPU増設遅延: {gpu_delay_months}ヶ月
- DAU減少推定: {estimated_dau_loss:,}名
- コンテンツモデレーション: {moderation_risk}

広告主対応チームおよびインフラチームは
上記リスクに基づく緩和策を検討してください。
```

## 参照データソース（Fabric テーブル）

| テーブル名 | DB | 説明 | 主要カラム |
|---|---|---|---|
| `sns_users` | sqldb_sns_01 | SNS事業のユーザーマスタ | `sns_user_id`, `display_name`, `account_tier`, `status` |
| `sns_subscriptions` | sqldb_sns_01 | サブスクリプション契約情報 | `subscription_id`, `sns_user_id`, `plan_code`, `monthly_fee`, `status` |
| `sns_transactions` | sqldb_sns_01 | 取引履歴 | `transaction_id`, `sns_user_id`, `transaction_type`, `amount`, `transaction_date` |
| `sns_ad_campaigns` | sqldb_sns_01 | 広告キャンペーン管理 | `campaign_id`, `advertiser_id`, `campaign_name`, `budget`, `spent_amount`, `status` |
| `unified_customers` | sqldb_common_01 | 統合顧客マスタ | `unified_customer_id`, `full_name`, `email`, `phone_number` |
| `domain_id_mappings` | sqldb_common_01 | 業務領域IDマッピング | `unified_customer_id`, `domain_code`, `domain_customer_id`, `is_active` |
| `customer_segments` | sqldb_common_01 | 顧客セグメント | `unified_customer_id`, `segment_code`, `risk_score`, `lifetime_value` |

## データ品質メモ

- `sns_transactions` の `transaction_type = 'AD_REVENUE'` を集計して `monthly_ad_revenue_jpy` を算出する。広告収益はリアルタイムでは取得できないため、前月実績値を基準とする。
- `sns_ad_campaigns` の `status = 'ACTIVE'` 件数が `active_ad_campaigns` に対応する。キャンペーン予算残（`budget - spent_amount`）が小さい案件は影響が限定的。
- `sns_users` の `status = 'ACTIVE'` かつ直近30日ログインのユーザー数がDAUの近似値となるが、正確なDAUはアクセスログ集計から取得すること。
- GPU利用率・サーバー利用率・広告配信レイテンシなどのリアルタイム指標はFabricテーブル外の監視システムから取得する前提。
- コンテンツモデレーションのGPU依存度が高い場合（画像・動画のAI判定）、GPU供給制約は安全性リスクに直結する。`account_tier = 'PREMIUM'` ユーザーのコンテンツを優先モデレーション対象とすることを推奨。
