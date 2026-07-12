# SNS事業：クラウドサイバー攻撃インパクト判断スキル

## 前提

- 本文書は nexus6-se-workshop の Demo 用ナレッジであり、数値・事例はすべて想定値である。
- 個人情報、実在企業の機密、実顧客の行動履歴は含めない。
- 対象事業部は SNS事業 である。
- 対応シナリオは「国内主要クラウドリージョンへの大規模サイバー攻撃（サイバーセキュリティリスク）」。
- Agent 2 は影響有無の評価、Agent 3 は事業部別 Next Action の草案作成に使う。

## 概要

国内大手クラウドプロバイダーの東京リージョンがDDoS攻撃とランサムウェアの複合攻撃を受けた際に、SNSプラットフォームの可用性低下、ユーザーエンゲージメントの毀損、広告配信停止による収益損失、ユーザーデータの漏洩・毀損リスク、およびプラットフォーム信頼性の低下に伴う競合流出リスクを総合的に判断するスキル。サービス停止時間、DAU影響、広告収益損失、データ安全性を定量評価し、推奨アクションを出力する。

## 適用シナリオ

- 外部ニュースのトリガー: クラウドプロバイダーの障害報告、大規模DDoS攻撃の報道、ランサムウェア被害報告、東京リージョンの複数AZ障害。
- SNS事業はリアルタイム性・常時可用性がユーザー体験の根幹であり、数時間の停止でもDAU離脱・広告収益逸失・レピュテーション毀損に直結する。
- プラットフォーム停止中はコンテンツモデレーションも停止するため、復旧直後の不適切コンテンツ流入リスクにも備える必要がある。

## 入力パラメータ

### ニュース情報

| パラメータ | 型 | 説明 |
|---|---|---|
| `news_category` | string | ニュースカテゴリ（`cyber_security`） |
| `attack_type` | list[string] | 攻撃種別（例: `DDoS`, `ransomware`, `supply_chain`, `zero_day`） |
| `affected_cloud_provider` | string | 影響クラウドプロバイダー名 |
| `affected_region` | string | 影響リージョン（例: `ap-northeast-1`） |
| `affected_az_count` | int | 影響AZ数 |
| `total_az_count` | int | 当該リージョン総AZ数 |
| `estimated_downtime_hours` | float | 推定障害時間（時間） |
| `data_breach_confirmed` | bool | データ漏洩の確認有無 |
| `attack_attribution` | enum | `nation_state` / `criminal_group` / `unknown` |

### 業務データ

| パラメータ | 型 | 説明 | 参照テーブル |
|---|---|---|---|
| `platform_hosted_on_affected_region` | bool | SNSプラットフォームが影響リージョン上で稼働しているか | インフラ台帳 |
| `platform_current_status` | enum | 現在のプラットフォーム状態: `operational`/`degraded`/`partial_outage`/`full_outage` | 監視システム |
| `dau` | int | 通常時DAU（日次アクティブユーザー数） | `sns_users` 集計 |
| `concurrent_users_peak` | int | ピーク時同時接続ユーザー数 | アクセスログ |
| `monthly_ad_revenue_jpy` | int | 月間広告収益（円） | `sns_transactions` (type=AD_REVENUE) |
| `active_ad_campaigns` | int | 稼働中の広告キャンペーン数 | `sns_ad_campaigns` (status=ACTIVE) |
| `active_subscriptions` | int | アクティブサブスクリプション数 | `sns_subscriptions` (status=ACTIVE) |
| `dr_site_available` | bool | DR環境（別リージョン）の利用可否 | インフラ台帳 |
| `dr_failover_time_minutes` | int | DR切替所要時間（分） | BCP計画書 |
| `cdn_independent` | bool | CDN がクラウドプロバイダーと独立しているか | インフラ台帳 |
| `user_data_backup_region` | string | ユーザーデータバックアップの保管リージョン | バックアップ管理 |
| `last_backup_hours_ago` | float | 最終バックアップからの経過時間 | バックアップ管理 |
| `content_moderation_backlog` | int | コンテンツモデレーション未処理件数 | モデレーションシステム |
| `push_notification_system_status` | enum | プッシュ通知システムの状態: `operational`/`down` | 監視システム |

## 判断ロジック

### Step 1: プラットフォーム可用性の判定

```
IF NOT platform_hosted_on_affected_region THEN
  platform_impact = "none"
  -- 影響リージョン外で稼働しているため直接影響なし
  -- ただし依存サービス（認証基盤、CDN等）の間接影響は Step 2 で評価
ELSE
  IF platform_current_status == "full_outage" THEN
    platform_impact = "total"
  ELSE IF platform_current_status == "partial_outage" THEN
    platform_impact = "major"
  ELSE IF platform_current_status == "degraded" THEN
    platform_impact = "partial"
  ELSE
    platform_impact = "monitoring"

az_impact_ratio = affected_az_count / total_az_count
```

### Step 2: ユーザー影響規模の算出

```
-- 停止時間帯によるDAU影響係数
-- SNSは24時間利用されるが、19:00-23:00がピーク
peak_hours_overlap = estimate_peak_overlap(estimated_downtime_hours)
  -- 簡易推定: downtime のうちピーク帯(4h)に重なる時間の比率

IF platform_impact == "total" THEN
  affected_users = dau
  engagement_loss_factor = 1.0
ELSE IF platform_impact == "major" THEN
  affected_users = dau × 0.7
  engagement_loss_factor = 0.7
ELSE IF platform_impact == "partial" THEN
  affected_users = dau × 0.3
  engagement_loss_factor = 0.3
ELSE
  affected_users = 0
  engagement_loss_factor = 0.0

-- 長時間停止による競合プラットフォームへの流出推定
IF estimated_downtime_hours >= 24 THEN
  competitor_migration_risk = "critical"
  estimated_permanent_dau_loss_pct = 3.0
ELSE IF estimated_downtime_hours >= 12 THEN
  competitor_migration_risk = "high"
  estimated_permanent_dau_loss_pct = 1.5
ELSE IF estimated_downtime_hours >= 4 THEN
  competitor_migration_risk = "medium"
  estimated_permanent_dau_loss_pct = 0.5
ELSE
  competitor_migration_risk = "low"
  estimated_permanent_dau_loss_pct = 0.1

estimated_permanent_dau_loss = dau × (estimated_permanent_dau_loss_pct / 100)
```

### Step 3: 広告収益損失の算出

```
daily_ad_revenue = monthly_ad_revenue_jpy / 30

-- 停止中の広告配信停止による直接損失
direct_ad_loss = daily_ad_revenue × (estimated_downtime_hours / 24) × engagement_loss_factor

-- 広告主SLA違反による補填リスク
IF estimated_downtime_hours >= 8 THEN
  sla_compensation_ratio = 0.10  -- 月額予算の10%補填
ELSE IF estimated_downtime_hours >= 4 THEN
  sla_compensation_ratio = 0.05
ELSE
  sla_compensation_ratio = 0.0

sla_compensation_jpy = active_ad_campaigns × (monthly_ad_revenue_jpy / MAX(1, active_ad_campaigns)) × sla_compensation_ratio

-- 復旧後の広告効果低下（ユーザー減少分）
post_recovery_monthly_loss = monthly_ad_revenue_jpy × (estimated_permanent_dau_loss_pct / 100)

total_revenue_impact = direct_ad_loss + sla_compensation_jpy + post_recovery_monthly_loss
```

### Step 4: データ安全性リスクの評価

```
IF data_breach_confirmed THEN
  data_risk = "critical"
  -- SNSはユーザーのDM、プロフィール、行動履歴等の機微データを保持
  sensitive_data_exposure = TRUE
ELSE IF "ransomware" IN attack_type AND user_data_backup_region == affected_region THEN
  data_risk = "high"
  sensitive_data_exposure = FALSE
ELSE IF "ransomware" IN attack_type AND last_backup_hours_ago > 24 THEN
  data_risk = "high"
  sensitive_data_exposure = FALSE
ELSE IF user_data_backup_region != affected_region AND last_backup_hours_ago <= 6 THEN
  data_risk = "low"
  sensitive_data_exposure = FALSE
ELSE
  data_risk = "medium"
  sensitive_data_exposure = FALSE

-- データ漏洩時の追加影響（個人情報保護法との複合リスク）
IF sensitive_data_exposure THEN
  regulatory_compound_risk = "critical"
  -- 改正個人情報保護法下での漏洩報告義務・制裁金リスクと複合
ELSE
  regulatory_compound_risk = "none"
```

### Step 5: DR復旧能力の評価

```
IF dr_site_available AND dr_failover_time_minutes <= 15 THEN
  recovery_capability = "strong"
  effective_downtime = MIN(estimated_downtime_hours, dr_failover_time_minutes / 60)
ELSE IF dr_site_available AND dr_failover_time_minutes <= 60 THEN
  recovery_capability = "moderate"
  effective_downtime = MIN(estimated_downtime_hours, dr_failover_time_minutes / 60 + 0.5)
ELSE IF dr_site_available THEN
  recovery_capability = "weak"
  effective_downtime = estimated_downtime_hours × 0.7
ELSE
  recovery_capability = "none"
  effective_downtime = estimated_downtime_hours

-- CDN独立性の補正
IF cdn_independent AND platform_impact != "total" THEN
  -- 静的コンテンツ配信は維持可能 → 部分的なUX維持
  cdn_mitigation = TRUE
  effective_engagement_loss = engagement_loss_factor × 0.7
ELSE
  cdn_mitigation = FALSE
  effective_engagement_loss = engagement_loss_factor
```

### Step 6: プラットフォーム信頼性・レピュテーション影響

```
-- SNS特有: ユーザーが障害をSNS上（または競合SNS上）で拡散するリスク
IF platform_impact IN ("total", "major") AND estimated_downtime_hours >= 2 THEN
  reputation_amplification = "high"
  -- 自社SNSが停止中の場合、競合SNSでネガティブ情報が拡散される
ELSE IF platform_impact == "partial" THEN
  reputation_amplification = "medium"
ELSE
  reputation_amplification = "low"

-- サブスクリプション解約リスク
IF platform_impact IN ("total", "major") AND estimated_downtime_hours >= 12 THEN
  subscription_churn_pct = 5.0
ELSE IF platform_impact IN ("total", "major") AND estimated_downtime_hours >= 4 THEN
  subscription_churn_pct = 2.0
ELSE
  subscription_churn_pct = 0.5

at_risk_subscriptions = active_subscriptions × (subscription_churn_pct / 100)
```

### Step 7: 総合インパクトスコア算出

```
platform_impact_weight:
  total      → 1.0
  major      → 0.7
  partial    → 0.4
  monitoring → 0.15
  none       → 0.0

data_risk_weight:
  critical → 1.0
  high     → 0.7
  medium   → 0.4
  low      → 0.1

recovery_factor:
  strong   → 0.3
  moderate → 0.55
  weak     → 0.75
  none     → 1.0

competitor_migration_weight:
  critical → 1.0
  high     → 0.7
  medium   → 0.4
  low      → 0.1

reputation_weight:
  high   → 1.0
  medium → 0.5
  low    → 0.1

raw_score = (platform_impact_weight × 0.25
           + data_risk_weight × 0.20
           + competitor_migration_weight × 0.15
           + reputation_weight × 0.10
           + recovery_factor × 0.20
           + MIN(1.0, total_revenue_impact / (monthly_ad_revenue_jpy × 0.3)) × 0.10)
           × 100

impact_score = CLAMP(raw_score, 0, 100)
```

### Step 8: インパクトレベル判定

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
| `platform_impact` | enum | プラットフォーム可用性影響（`total`/`major`/`partial`/`monitoring`/`none`） |
| `effective_downtime_hours` | float | 実効停止時間（DR切替考慮後） |
| `affected_users` | int | 影響ユーザー数 |
| `estimated_permanent_dau_loss` | int | 恒久的DAU流出推定数 |
| `total_revenue_impact_jpy` | int | 総収益影響額（直接損失＋SLA補填＋復旧後減収） |
| `data_risk_level` | enum | データ安全性リスクレベル |
| `recovery_capability` | enum | DR復旧能力 |
| `competitor_migration_risk` | enum | 競合プラットフォーム流出リスク |
| `at_risk_subscriptions` | int | 解約リスクのあるサブスクリプション数 |
| `recommended_actions` | list[string] | 推奨アクションリスト |
| `notification_channel` | string | 推奨通知チャネル |
| `risk_summary` | string | リスクサマリー文（自然言語） |

### 推奨アクション分岐

| レベル | 推奨アクション | 通知チャネル |
|---|---|---|
| critical | DR環境への即時フェイルオーバー実行、ステータスページの即時更新（多言語対応）、競合SNS上での公式アカウントによる障害告知投稿、広告配信の全面一時停止と広告主への自動通知発信、CSIRT起動とデータ漏洩フォレンジック調査の開始、プッシュ通知（利用可能な場合）でのユーザー告知、コンテンツモデレーションの復旧時即時強化体制の準備、経営層・IR部門への即時エスカレーション | slack + email |
| high | DR切替準備の即時開始、ステータスページの更新、広告配信のペーシング調整（影響地域向け停止）、広告主への状況報告メールの準備、コンテンツモデレーション代替手段（ルールベース）の有効化、ユーザー向けアプリ内バナーでの障害告知準備、セキュリティログの緊急監査 | slack |
| medium | プラットフォーム稼働監視の強化（1分間隔）、DR切替手順の確認・テスト、広告配信品質の継続監視、クラウドプロバイダーへの状況確認と復旧見通し取得、SNS上のユーザー反応モニタリング開始 | slack |
| low | クラウドプロバイダーの復旧状況モニタリング、マルチクラウド/マルチリージョン戦略の見直し検討、BCP/DR訓練の計画立案 | 定期レポート |

### 通知テンプレート（critical / high 時）

```
件名: 【{impact_level}】クラウド攻撃によるSNSプラットフォーム停止リスク
本文:
クラウドリージョンへのサイバー攻撃に伴い、
SNSプラットフォームの可用性に重大な影響が発生しています。
- インパクトスコア: {impact_score}/100
- プラットフォーム状態: {platform_impact}
- 実効停止時間: {effective_downtime_hours:.1f}時間
- 影響ユーザー数: {affected_users:,}名
- 恒久DAU流出リスク: {estimated_permanent_dau_loss:,}名（{estimated_permanent_dau_loss_pct:.1f}%）
- 広告収益影響: ¥{total_revenue_impact_jpy:,}
- 解約リスクサブスク: {at_risk_subscriptions:,}件
- データリスク: {data_risk_level}
- DR復旧能力: {recovery_capability}

ステータスページの即時更新と、競合SNS上での
公式障害告知を最優先で実施してください。
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

- `sns_users` の `status = 'ACTIVE'` かつ `last_login_at` が直近30日以内のユーザー数がDAUの近似値。正確なDAU・同時接続数はアクセスログ集計システムから取得すること。
- `sns_transactions` の `transaction_type = 'AD_REVENUE'` を日次集計して `daily_ad_revenue` を算出する。停止中の逸失収益算出に使用。
- `sns_ad_campaigns` の `status = 'ACTIVE'` かつ `end_date >= TODAY` のキャンペーン数が `active_ad_campaigns`。SLA補填対象の判定に使用。
- `sns_subscriptions` の `status = 'ACTIVE'` 件数が `active_subscriptions`。長時間停止時のサブスクリプション解約リスク算出に使用。
- プラットフォームの稼働状態、DR環境の可否、CDN独立性などのインフラ情報はFabricテーブル外の監視システム・インフラ台帳から取得する前提。
- SNS特有の留意点: 攻撃中は自社プラットフォーム上でのユーザーコミュニケーションが不可能なため、競合SNS・メール・プッシュ通知など代替チャネルでの告知手段の確保が最優先。`push_notification_system_status` が `operational` かどうかを必ず確認する。
- `customer_segments.segment_code = 'PREMIUM'` のユーザーは有料サブスクリプション契約者との重複が高く、解約リスクの優先評価対象とする。
