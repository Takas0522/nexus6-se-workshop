# 携帯電話事業：クラウドサイバー攻撃インパクト判断スキル

## 前提

- 本文書は nexus6-se-workshop の Demo 用ナレッジであり、数値・事例はすべて想定値である。
- 個人情報、実在企業の機密、実顧客の行動履歴は含めない。
- 対象事業部は 携帯電話事業 である。
- 対応シナリオは「国内主要クラウドリージョンへの大規模サイバー攻撃（サイバーセキュリティリスク）」。
- Agent 2 は影響有無の評価、Agent 3 は事業部別 Next Action の草案作成に使う。

## 概要

国内大手クラウドプロバイダーの東京リージョンがDDoS攻撃とランサムウェアの複合攻撃を受けた際に、携帯電話事業のオンライン契約システム・マイページサービスの可用性低下、顧客対応チャネル切替の必要性、データ漏洩リスク、およびレピュテーション影響を総合的に判断するスキル。サービス停止時間、影響顧客数、代替チャネルの準備状況を定量評価し、推奨アクションを出力する。

## 適用シナリオ

- 外部ニュースのトリガー: クラウドプロバイダーの障害報告、大規模DDoS攻撃の報道、ランサムウェア被害報告、東京リージョンの複数AZ障害。
- ニュースは判断の入口であり、最終判断は自社サービスの稼働状況データで行う。
- 速報段階では 影響を受けるサービスの一覧、現在の稼働状況、DR切替可否 を即時参照する。
- 事後評価では 停止時間の合計、影響顧客数、問い合わせ増加率、データ安全性確認結果 を含めて再評価する。

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
| `services_on_affected_region` | int | 影響リージョン上の自社サービス数 | インフラ台帳 |
| `services_currently_down` | int | 現在停止中のサービス数 | 監視システム |
| `online_contract_daily_volume` | int | オンライン契約の日次処理件数 | `mobile_contracts` 集計 |
| `mypage_daily_active_users` | int | マイページ日次アクティブユーザー数 | アクセスログ集計 |
| `dr_site_available` | bool | DR環境（別リージョン）の利用可否 | インフラ台帳 |
| `dr_failover_time_minutes` | int | DR切替所要時間（分） | BCP計画書 |
| `store_branch_count` | int | 店舗窓口数（代替チャネル） | `mobile_customers` 関連マスタ |
| `callcenter_current_capacity` | int | コールセンター現在の受電可能件数/時 | コールセンター管理 |
| `callcenter_avg_volume` | int | コールセンター通常時の受電件数/時 | コールセンター管理 |
| `active_customer_count` | int | アクティブ顧客数 | `mobile_customers` (status=ACTIVE) |
| `backup_data_region` | string | バックアップデータの保管リージョン | インフラ台帳 |
| `last_backup_hours_ago` | float | 最終バックアップからの経過時間 | バックアップ管理 |

## 判断ロジック

### Step 1: サービス影響範囲の判定

```
az_impact_ratio = affected_az_count / total_az_count
service_down_ratio = services_currently_down / services_on_affected_region

IF service_down_ratio >= 0.8 THEN service_impact = "total"
ELSE IF service_down_ratio >= 0.5 THEN service_impact = "major"
ELSE IF service_down_ratio > 0 THEN service_impact = "partial"
ELSE service_impact = "none"
```

### Step 2: 顧客影響規模の算出

```
-- オンライン契約停止による影響
lost_contracts_per_hour = online_contract_daily_volume / 8
total_lost_contracts = lost_contracts_per_hour × estimated_downtime_hours

-- マイページ停止による影響顧客数
affected_mypage_users = mypage_daily_active_users × MIN(1.0, estimated_downtime_hours / 24)

-- コールセンターへの波及
estimated_call_surge_multiplier:
  service_impact "total" → 4.0
  service_impact "major" → 2.5
  service_impact "partial" → 1.5
  service_impact "none" → 1.0

estimated_call_volume = callcenter_avg_volume × estimated_call_surge_multiplier
call_overflow = MAX(0, estimated_call_volume - callcenter_current_capacity)
```

### Step 3: DR切替可否と復旧時間の評価

```
IF dr_site_available AND dr_failover_time_minutes <= 30 THEN
  recovery_capability = "strong"
  effective_downtime = MIN(estimated_downtime_hours, dr_failover_time_minutes / 60)
ELSE IF dr_site_available AND dr_failover_time_minutes <= 120 THEN
  recovery_capability = "moderate"
  effective_downtime = MIN(estimated_downtime_hours, dr_failover_time_minutes / 60 + 0.5)
ELSE IF dr_site_available THEN
  recovery_capability = "weak"
  effective_downtime = estimated_downtime_hours × 0.7
ELSE
  recovery_capability = "none"
  effective_downtime = estimated_downtime_hours
```

### Step 4: データ安全性リスクの評価

```
IF data_breach_confirmed THEN data_risk = "critical"
ELSE IF "ransomware" IN attack_type AND backup_data_region == affected_region THEN
  data_risk = "high"
ELSE IF "ransomware" IN attack_type AND last_backup_hours_ago > 24 THEN
  data_risk = "medium"
ELSE IF backup_data_region != affected_region AND last_backup_hours_ago <= 6 THEN
  data_risk = "low"
ELSE
  data_risk = "medium"
```

### Step 5: 攻撃の持続性・再発リスクの評価

```
persistence_risk:
  attack_attribution "nation_state" AND "ransomware" IN attack_type → "critical"
  attack_attribution "nation_state" → "high"
  attack_attribution "criminal_group" AND LEN(attack_type) >= 2 → "high"
  attack_attribution "criminal_group" → "medium"
  attack_attribution "unknown" → "medium"

persistence_weight:
  critical → 0.30
  high     → 0.20
  medium   → 0.10
```

### Step 6: 総合インパクトスコア算出

```
service_impact_weight:
  total   → 1.0
  major   → 0.7
  partial → 0.4
  none    → 0.0

data_risk_weight:
  critical → 1.0
  high     → 0.7
  medium   → 0.4
  low      → 0.1

recovery_factor:
  strong   → 0.4
  moderate → 0.65
  weak     → 0.8
  none     → 1.0

customer_impact_ratio = (affected_mypage_users + total_lost_contracts) / active_customer_count
customer_factor = MIN(1.0, customer_impact_ratio × 5)

raw_score = (service_impact_weight × 0.25
           + data_risk_weight × 0.20
           + customer_factor × 0.20
           + persistence_weight × 0.10
           + (1.0 - (1.0 - recovery_factor)) × 0.25)
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
| `service_impact_level` | enum | サービス影響レベル（`total`/`major`/`partial`/`none`） |
| `effective_downtime_hours` | float | 実効停止時間（DR切替考慮後） |
| `affected_customer_count` | int | 影響顧客数 |
| `lost_contracts_estimate` | int | 契約処理損失推定件数 |
| `call_overflow_per_hour` | int | コールセンター溢れ件数/時 |
| `data_risk_level` | enum | データ安全性リスクレベル |
| `recovery_capability` | enum | DR復旧能力 |
| `recommended_actions` | list[string] | 推奨アクションリスト |
| `notification_channel` | string | 推奨通知チャネル |
| `risk_summary` | string | リスクサマリー文（自然言語） |

### 推奨アクション分岐

| レベル | 推奨アクション | 通知チャネル |
|---|---|---|
| critical | DR環境への即時フェイルオーバー実行、全店舗窓口への代替対応指示発出、コールセンター緊急増員の手配、顧客向け障害告知ページの公開、経営層・CSIRT へのエスカレーション、データ漏洩フォレンジック調査の開始、WAFルール緊急強化・特権アカウントのパスワードリセット | slack + email |
| high | DR切替準備の即時開始、店舗窓口での代替受付体制の確保、コールセンター人員配置の見直し、ユーザー向けSNS・メール告知の準備、EDRログの集中監視開始、VPN接続経路のセキュリティ点検 | slack |
| medium | サービス稼働状況の監視強化（5分間隔）、コールセンター待機要員の確保、クラウドプロバイダーへの状況確認、店舗窓口への事前連絡、二次攻撃に備えたセキュリティアラート発出 | slack |
| low | クラウドプロバイダーの復旧状況モニタリング、BCP計画の見直しポイント記録、次回防災訓練への反映事項整理 | 定期レポート |

### 通知テンプレート（critical / high 時）

```
件名: 【{impact_level}】クラウド攻撃によるオンラインサービス停止リスク
本文:
クラウドリージョンへの攻撃に伴い、オンライン契約システム
およびマイページサービスが停止する可能性があります。
- インパクトスコア: {impact_score}/100
- サービス影響: {service_impact_level}（停止中: {services_currently_down}件）
- 実効停止時間: {effective_downtime_hours:.1f}時間
- 影響顧客数: {affected_customer_count:,}名
- データリスク: {data_risk_level}
- DR復旧能力: {recovery_capability}

店舗窓口での代替対応体制を確保し、コールセンターへの
問い合わせ増加に備えた人員配置をお願いします。
```

## 参照データソース（Fabric テーブル）

| テーブル名 | DB | 説明 | 主要カラム |
|---|---|---|---|
| `mobile_customers` | sqldb_mobile_01 | 携帯電話事業の顧客マスタ | `mobile_customer_id`, `customer_name`, `customer_type`, `status` |
| `mobile_contracts` | sqldb_mobile_01 | 回線契約情報 | `contract_id`, `mobile_customer_id`, `plan_code`, `contract_start_date`, `status` |
| `mobile_transactions` | sqldb_mobile_01 | 取引履歴 | `transaction_id`, `mobile_customer_id`, `transaction_type`, `amount`, `transaction_date` |
| `mobile_device_inventory` | sqldb_mobile_01 | 端末在庫管理 | `inventory_id`, `device_model`, `stock_quantity`, `status` |
| `unified_customers` | sqldb_common_01 | 統合顧客マスタ | `unified_customer_id`, `full_name`, `email`, `phone_number` |
| `domain_id_mappings` | sqldb_common_01 | 業務領域IDマッピング | `unified_customer_id`, `domain_code`, `domain_customer_id`, `is_active` |
| `customer_segments` | sqldb_common_01 | 顧客セグメント | `unified_customer_id`, `segment_code`, `risk_score`, `lifetime_value` |

## データ品質メモ

- `mobile_contracts` の日次新規件数（`contract_start_date` = 当日）がオンライン契約数の近似値となる。チャネル別の内訳は別途チャネル管理台帳が必要。
- コールセンターのキャパシティ・実績データは DB 外のコールセンター管理システムから取得する前提。
- DR関連の情報（切替時間、バックアップリージョン等）はインフラ台帳・BCP計画書から取得する前提で、Fabric テーブルには格納されていない。
- `customer_segments.segment_code = 'PREMIUM'` の顧客はオンラインサービス利用率が高い傾向があり、停止時の影響・問い合わせ増加が顕著。セグメント別の影響分析を推奨。
- 攻撃中はデータベース自体へのアクセスが制限される可能性がある。オフラインでの判断ロジック実行を想定し、直近のキャッシュデータでの評価も許容する。
