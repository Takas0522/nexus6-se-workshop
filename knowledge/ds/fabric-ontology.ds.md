# DS: Fabric オントロジー定義 — ニュース分析・インパクト診断システム

## 前提

- 本文書は nexus6-se-workshop の Demo 用 DS.md である。
- 数値例、テーブル名、業務事例は架空であり、実在企業の機密を含まない。
- Agent 2・3 が Fabric クエリを組み立てる前に参照する。
- SQL の方言は Fabric SQL Analytics Endpoint を想定する。
- 対象データベース: `sqldb_common_01`, `sqldb_mobile_01`, `sqldb_sns_01`, `sqldb_si_01`。

## データソース概要

本システムは、3つの業務領域（携帯電話事業・SNS事業・SI事業）と統合顧客管理基盤で構成される4つのデータベースを持つ。ニュース分析エージェントは、外部ニュースの内容を起点として各業務領域のデータを横断的に参照し、ビジネスインパクトを定量的に評価する。

| データベース | 業務領域 | テーブル数 | 説明 |
|---|---|---|---|
| `sqldb_common_01` | 統合管理 | 3 | 全業務領域を横断する顧客マスタ・セグメント・IDマッピング |
| `sqldb_mobile_01` | 携帯電話事業 | 4 | 顧客・回線契約・取引履歴・端末在庫 |
| `sqldb_sns_01` | SNS事業 | 4 | ユーザー・サブスクリプション・取引履歴・広告キャンペーン |
| `sqldb_si_01` | SI事業 | 5 | 法人顧客・プロジェクト・契約・取引履歴・エンジニアリソース |

### データ規模

| 指標 | 値 |
|---|---|
| 総顧客数 | 9,000 |
| 月間トランザクション（携帯電話） | 18,000 |
| 月間トランザクション（SNS） | 45,000 |
| 月間トランザクション（SI） | 27,000 |
| 対象期間 | 6ヶ月 |
| 総レコード数 | 540,000 |

---

## エンティティ定義

### 統合管理基盤（sqldb_common_01）

#### unified_customers — 統合顧客マスタ

全業務領域の顧客を一意に識別し一元管理するマスタエンティティ。各業務領域の顧客は `domain_id_mappings` を介して本テーブルと紐づく。

| カラム | 型 | PK | 業務意味 |
|---|---|---|---|
| `unified_customer_id` | UNIQUEIDENTIFIER | ✓ | 統合顧客ID |
| `full_name` | NVARCHAR(200) | | 氏名 |
| `email` | NVARCHAR(256) | | メールアドレス |
| `phone_number` | NVARCHAR(20) | | 電話番号 |
| `date_of_birth` | DATE | | 生年月日 |
| `postal_code` | NVARCHAR(10) | | 郵便番号 |
| `address` | NVARCHAR(500) | | 住所 |
| `created_at` | DATETIME2 | | 作成日時 |
| `updated_at` | DATETIME2 | | 更新日時 |

**業務ルール**: 1顧客が複数の業務領域にまたがって登録されている場合でも、`unified_customer_id` は1つ。名寄せ済みの状態を前提とする。

#### domain_id_mappings — 業務領域IDマッピング

統合顧客IDと各業務領域固有の顧客IDを対応付けるブリッジエンティティ。1つの `unified_customer_id` に対して最大3件（MOBILE / SNS / SI）のマッピングが存在する。

| カラム | 型 | PK | 業務意味 |
|---|---|---|---|
| `mapping_id` | UNIQUEIDENTIFIER | ✓ | マッピングID |
| `unified_customer_id` | UNIQUEIDENTIFIER | | 統合顧客ID（FK → unified_customers） |
| `domain_code` | NVARCHAR(20) | | 業務領域コード（`MOBILE` / `SNS` / `SI`） |
| `domain_customer_id` | NVARCHAR(50) | | 業務領域固有の顧客ID |
| `is_active` | BIT | | 有効フラグ（1=有効, 0=無効） |
| `linked_at` | DATETIME2 | | 紐付け日時 |

**業務ルール**: `is_active = 1` のレコードのみが有効なマッピング。業務領域を退会した場合は `is_active = 0` に更新される（物理削除しない）。

#### customer_segments — 顧客セグメント

マーケティング・リスク分析に使用する顧客セグメント情報。定期バッチで評価・更新される。

| カラム | 型 | PK | 業務意味 |
|---|---|---|---|
| `segment_id` | UNIQUEIDENTIFIER | ✓ | セグメントID |
| `unified_customer_id` | UNIQUEIDENTIFIER | | 統合顧客ID（FK → unified_customers） |
| `segment_code` | NVARCHAR(30) | | セグメントコード（`PREMIUM` / `STANDARD` / `BASIC` / `AT_RISK`） |
| `risk_score` | DECIMAL(5,2) | | リスクスコア（0.00〜100.00。高いほど離脱リスクが高い） |
| `lifetime_value` | DECIMAL(12,2) | | 生涯価値（円） |
| `evaluated_at` | DATETIME2 | | 評価日時 |

**業務ルール**: `segment_code = 'PREMIUM'` の顧客は全スキルで優先対応対象となる。`risk_score >= 70` は要注意顧客。

---

### 携帯電話事業（sqldb_mobile_01）

#### mobile_customers — 携帯電話事業顧客マスタ

| カラム | 型 | PK | 業務意味 |
|---|---|---|---|
| `mobile_customer_id` | NVARCHAR(50) | ✓ | 携帯電話事業の顧客ID |
| `customer_name` | NVARCHAR(200) | | 顧客名 |
| `customer_type` | NVARCHAR(20) | | 顧客種別（`INDIVIDUAL` / `CORPORATE`） |
| `status` | NVARCHAR(20) | | ステータス（`ACTIVE` / `SUSPENDED` / `TERMINATED`） |
| `registered_at` | DATETIME2 | | 登録日時 |
| `updated_at` | DATETIME2 | | 更新日時 |

#### mobile_contracts — 回線契約情報

| カラム | 型 | PK | 業務意味 |
|---|---|---|---|
| `contract_id` | UNIQUEIDENTIFIER | ✓ | 契約ID |
| `mobile_customer_id` | NVARCHAR(50) | | 顧客ID（FK → mobile_customers） |
| `plan_code` | NVARCHAR(30) | | プランコード |
| `phone_number` | NVARCHAR(20) | | 電話番号 |
| `sim_serial` | NVARCHAR(30) | | SIMシリアル番号 |
| `contract_start_date` | DATE | | 契約開始日 |
| `contract_end_date` | DATE | | 契約終了日 |
| `monthly_fee` | DECIMAL(10,2) | | 月額料金（円） |
| `status` | NVARCHAR(20) | | 契約ステータス（`ACTIVE` / `EXPIRED` / `CANCELLED`） |

**業務ルール**: 1顧客が複数回線を保有可能（1:N）。`contract_start_date` が当日のレコードがオンライン契約の日次件数の近似値。

#### mobile_transactions — 取引履歴

| カラム | 型 | PK | 業務意味 |
|---|---|---|---|
| `transaction_id` | UNIQUEIDENTIFIER | ✓ | 取引ID |
| `mobile_customer_id` | NVARCHAR(50) | | 顧客ID（FK → mobile_customers） |
| `contract_id` | UNIQUEIDENTIFIER | | 契約ID（FK → mobile_contracts） |
| `transaction_type` | NVARCHAR(30) | | 取引種別（`VOICE` / `DATA` / `DEVICE_PURCHASE` / `MONTHLY_FEE` / `PREMIUM_SERVICE` / `OVERAGE`） |
| `amount` | DECIMAL(12,2) | | 金額（円） |
| `currency` | NVARCHAR(3) | | 通貨コード（`JPY`） |
| `transaction_date` | DATETIME2 | | 取引日時 |
| `description` | NVARCHAR(500) | | 取引詳細 |

#### mobile_device_inventory — 端末在庫管理

| カラム | 型 | PK | 業務意味 |
|---|---|---|---|
| `inventory_id` | UNIQUEIDENTIFIER | ✓ | 在庫ID |
| `device_model` | NVARCHAR(100) | | 端末モデル名 |
| `manufacturer` | NVARCHAR(100) | | メーカー名 |
| `imei` | NVARCHAR(20) | | IMEI |
| `stock_quantity` | INT | | 在庫数量 |
| `unit_cost` | DECIMAL(10,2) | | 仕入単価（円） |
| `warehouse_code` | NVARCHAR(20) | | 倉庫コード |
| `status` | NVARCHAR(20) | | 在庫ステータス（`IN_STOCK` / `RESERVED` / `SOLD` / `DEFECTIVE`） |
| `updated_at` | DATETIME2 | | 更新日時 |

**業務ルール**: `status = 'IN_STOCK'` の `stock_quantity` 合計が販売可能在庫。半導体供給制約スキルで在庫水準を評価する際に使用。

---

### SNS事業（sqldb_sns_01）

#### sns_users — ユーザーマスタ

| カラム | 型 | PK | 業務意味 |
|---|---|---|---|
| `sns_user_id` | NVARCHAR(50) | ✓ | SNSユーザーID |
| `display_name` | NVARCHAR(100) | | 表示名 |
| `email` | NVARCHAR(256) | | メールアドレス |
| `account_tier` | NVARCHAR(20) | | アカウント種別（`FREE` / `PREMIUM` / `BUSINESS`） |
| `status` | NVARCHAR(20) | | ステータス（`ACTIVE` / `SUSPENDED` / `DELETED`） |
| `registered_at` | DATETIME2 | | 登録日時 |
| `last_login_at` | DATETIME2 | | 最終ログイン日時 |

**業務ルール**: `status = 'ACTIVE'` かつ `last_login_at` が直近30日以内のユーザーがDAUの近似母集団。

#### sns_subscriptions — サブスクリプション契約

| カラム | 型 | PK | 業務意味 |
|---|---|---|---|
| `subscription_id` | UNIQUEIDENTIFIER | ✓ | サブスクリプションID |
| `sns_user_id` | NVARCHAR(50) | | ユーザーID（FK → sns_users） |
| `plan_code` | NVARCHAR(30) | | プランコード |
| `monthly_fee` | DECIMAL(10,2) | | 月額料金（円） |
| `start_date` | DATE | | 開始日 |
| `end_date` | DATE | | 終了日 |
| `auto_renew` | BIT | | 自動更新フラグ |
| `status` | NVARCHAR(20) | | ステータス（`ACTIVE` / `CANCELLED` / `EXPIRED`） |

#### sns_transactions — 取引履歴

| カラム | 型 | PK | 業務意味 |
|---|---|---|---|
| `transaction_id` | UNIQUEIDENTIFIER | ✓ | 取引ID |
| `sns_user_id` | NVARCHAR(50) | | ユーザーID（FK → sns_users） |
| `transaction_type` | NVARCHAR(30) | | 取引種別（`AD_REVENUE` / `SUBSCRIPTION` / `POINT_PURCHASE` / `GIFT` / `PREMIUM_CONTENT` / `TIP`） |
| `amount` | DECIMAL(12,2) | | 金額（円） |
| `currency` | NVARCHAR(3) | | 通貨コード |
| `transaction_date` | DATETIME2 | | 取引日時 |
| `related_content_id` | NVARCHAR(50) | | 関連コンテンツID |
| `description` | NVARCHAR(500) | | 取引詳細 |

**業務ルール**: `transaction_type = 'AD_REVENUE'` の月次集計が広告収益。個人情報保護法スキルでのターゲティング広告収益比率の算出に使用。

#### sns_ad_campaigns — 広告キャンペーン管理

| カラム | 型 | PK | 業務意味 |
|---|---|---|---|
| `campaign_id` | UNIQUEIDENTIFIER | ✓ | キャンペーンID |
| `advertiser_id` | NVARCHAR(50) | | 広告主ID |
| `campaign_name` | NVARCHAR(200) | | キャンペーン名 |
| `budget` | DECIMAL(12,2) | | 予算（円） |
| `spent_amount` | DECIMAL(12,2) | | 消化金額（円） |
| `start_date` | DATE | | 開始日 |
| `end_date` | DATE | | 終了日 |
| `status` | NVARCHAR(20) | | ステータス（`ACTIVE` / `PAUSED` / `COMPLETED` / `CANCELLED`） |

**業務ルール**: `status = 'ACTIVE'` かつ `end_date >= TODAY` が稼働中キャンペーン。`advertiser_id` のDISTINCT集計がアクティブ広告主数。

---

### SI事業（sqldb_si_01）

#### si_customers — 法人顧客マスタ

| カラム | 型 | PK | 業務意味 |
|---|---|---|---|
| `si_customer_id` | NVARCHAR(50) | ✓ | SI顧客ID |
| `company_name` | NVARCHAR(200) | | 法人名 |
| `industry_code` | NVARCHAR(10) | | 業種コード（`FINANCE` / `RETAIL` / `HEALTHCARE` / `MFG` / `GOV` / `TELECOM` / `MEDIA` / `LOGISTICS` / `ENERGY` / `EDUCATION` / `REAL_ESTATE` / `OTHER`） |
| `contact_name` | NVARCHAR(200) | | 担当者名 |
| `contact_email` | NVARCHAR(256) | | 担当者メール |
| `status` | NVARCHAR(20) | | ステータス（`ACTIVE` / `INACTIVE` / `PROSPECT`） |
| `registered_at` | DATETIME2 | | 登録日時 |
| `updated_at` | DATETIME2 | | 更新日時 |

**業務ルール**: `industry_code` は個人情報保護法スキルで影響業種の特定に使用する。`FINANCE`, `RETAIL`, `HEALTHCARE` は法改正の影響が大きい業種。

#### si_projects — プロジェクト管理

| カラム | 型 | PK | 業務意味 |
|---|---|---|---|
| `project_id` | UNIQUEIDENTIFIER | ✓ | プロジェクトID |
| `si_customer_id` | NVARCHAR(50) | | 顧客ID（FK → si_customers） |
| `project_name` | NVARCHAR(300) | | プロジェクト名 |
| `project_type` | NVARCHAR(30) | | プロジェクト種別（`INFRASTRUCTURE` / `CLOUD_MIGRATION` / `DATA_PLATFORM` / `CRM` / `ERP` / `SECURITY` / `ANALYTICS` / `MANAGED_SERVICE` / `CUSTOM_DEV`） |
| `contract_amount` | DECIMAL(14,2) | | 契約金額（円） |
| `start_date` | DATE | | 開始日 |
| `end_date` | DATE | | 終了日 |
| `status` | NVARCHAR(20) | | ステータス（`PLANNING` / `IN_PROGRESS` / `MAINTENANCE` / `COMPLETED` / `CANCELLED`） |
| `assigned_engineer_count` | INT | | アサインエンジニア数 |

**業務ルール**: `status = 'IN_PROGRESS'` が進行中、`status = 'MAINTENANCE'` が運用保守フェーズ。`project_type` に `INFRASTRUCTURE`, `CLOUD_MIGRATION` を含むプロジェクトがHW調達対象の候補。

#### si_contracts — 契約情報

| カラム | 型 | PK | 業務意味 |
|---|---|---|---|
| `contract_id` | UNIQUEIDENTIFIER | ✓ | 契約ID |
| `si_customer_id` | NVARCHAR(50) | | 顧客ID（FK → si_customers） |
| `project_id` | UNIQUEIDENTIFIER | | プロジェクトID（FK → si_projects） |
| `contract_type` | NVARCHAR(30) | | 契約種別（`FIXED_PRICE` / `TIME_AND_MATERIAL` / `SLA` / `MANAGED_SERVICE` / `MAINTENANCE`） |
| `total_amount` | DECIMAL(14,2) | | 契約金額（円） |
| `start_date` | DATE | | 開始日 |
| `end_date` | DATE | | 終了日 |
| `status` | NVARCHAR(20) | | ステータス（`ACTIVE` / `EXPIRED` / `TERMINATED`） |

#### si_transactions — 取引履歴

| カラム | 型 | PK | 業務意味 |
|---|---|---|---|
| `transaction_id` | UNIQUEIDENTIFIER | ✓ | 取引ID |
| `si_customer_id` | NVARCHAR(50) | | 顧客ID（FK → si_customers） |
| `contract_id` | UNIQUEIDENTIFIER | | 契約ID（FK → si_contracts） |
| `transaction_type` | NVARCHAR(30) | | 取引種別（`INVOICE` / `PAYMENT` / `TIMESHEET` / `HW_PROCUREMENT` / `LICENSE` / `EXPENSE`） |
| `amount` | DECIMAL(14,2) | | 金額（円） |
| `currency` | NVARCHAR(3) | | 通貨コード |
| `transaction_date` | DATETIME2 | | 取引日時 |
| `description` | NVARCHAR(500) | | 取引詳細 |

**業務ルール**: `transaction_type = 'HW_PROCUREMENT'` の合計がHW調達実績額。半導体供給制約スキルで調達コスト増加の算出に使用。

#### si_engineers — エンジニアリソース管理

| カラム | 型 | PK | 業務意味 |
|---|---|---|---|
| `engineer_id` | UNIQUEIDENTIFIER | ✓ | エンジニアID |
| `employee_name` | NVARCHAR(200) | | 氏名 |
| `skill_set` | NVARCHAR(1000) | | 保有スキル（JSON配列） |
| `grade` | NVARCHAR(10) | | グレード（`J1`〜`J3`, `M1`〜`M3`, `S1`〜`S3`, `E1`） |
| `hourly_rate` | DECIMAL(10,2) | | 時間単価（円） |
| `availability_status` | NVARCHAR(20) | | 稼働状況（`ASSIGNED` / `AVAILABLE` / `ON_CALL` / `ON_LEAVE`） |
| `current_project_id` | UNIQUEIDENTIFIER | | 現在アサイン先PJ（FK → si_projects、NULLable） |

**業務ルール**: `skill_set` はJSON配列（例: `["Java","AWS","security"]`）。プライバシー資格保有者は `privacy`, `security`, `ISMS`, `CIPP` のキーワードで検索。サイバー攻撃スキルでは `cloud`, `aws`, `azure`, `incident_response` で緊急対応要員を特定。

---

## リレーション定義

### ER図（論理構造）

```
sqldb_common_01                    sqldb_mobile_01
┌─────────────────┐               ┌────────────────────┐
│unified_customers│──1:N──────────│domain_id_mappings  │
│                 │──1:1──────────│customer_segments   │
└────────┬────────┘               └──────┬─────────────┘
         │                               │ domain_customer_id
         │ (MOBILE)                      ↓
         │                        ┌────────────────────┐
         │                        │ mobile_customers   │──1:N──→ mobile_contracts
         │                        │                    │──1:N──→ mobile_transactions
         │                        └────────────────────┘
         │                        ┌────────────────────┐
         │                        │mobile_device_inv.  │ (独立マスタ)
         │                        └────────────────────┘
         │
         │ (SNS)                  sqldb_sns_01
         │                        ┌────────────────────┐
         ├───────────────────────→│ sns_users          │──1:N──→ sns_subscriptions
         │                        │                    │──1:N──→ sns_transactions
         │                        └────────────────────┘
         │                        ┌────────────────────┐
         │                        │ sns_ad_campaigns   │ (advertiser_id で間接参照)
         │                        └────────────────────┘
         │
         │ (SI)                   sqldb_si_01
         │                        ┌────────────────────┐
         └───────────────────────→│ si_customers       │──1:N──→ si_projects ──1:N──→ si_contracts
                                  │                    │──1:N──→ si_transactions
                                  └────────────────────┘
                                  ┌────────────────────┐
                                  │ si_engineers       │──N:1──→ si_projects (current_project_id)
                                  └────────────────────┘
```

### 結合キー一覧

| 関係 | 親テーブル | 子テーブル | 結合キー | カーディナリティ |
|---|---|---|---|---|
| 統合顧客 → IDマッピング | unified_customers | domain_id_mappings | `unified_customer_id` | 1:N (最大3) |
| 統合顧客 → セグメント | unified_customers | customer_segments | `unified_customer_id` | 1:1 |
| IDマッピング → 携帯電話顧客 | domain_id_mappings | mobile_customers | `domain_customer_id = mobile_customer_id` (WHERE `domain_code = 'MOBILE'`) | 1:1 |
| IDマッピング → SNSユーザー | domain_id_mappings | sns_users | `domain_customer_id = sns_user_id` (WHERE `domain_code = 'SNS'`) | 1:1 |
| IDマッピング → SI顧客 | domain_id_mappings | si_customers | `domain_customer_id = si_customer_id` (WHERE `domain_code = 'SI'`) | 1:1 |
| 携帯電話顧客 → 契約 | mobile_customers | mobile_contracts | `mobile_customer_id` | 1:N |
| 携帯電話顧客 → 取引 | mobile_customers | mobile_transactions | `mobile_customer_id` | 1:N |
| 契約 → 取引 | mobile_contracts | mobile_transactions | `contract_id` | 1:N |
| SNSユーザー → サブスク | sns_users | sns_subscriptions | `sns_user_id` | 1:N |
| SNSユーザー → 取引 | sns_users | sns_transactions | `sns_user_id` | 1:N |
| SI顧客 → PJ | si_customers | si_projects | `si_customer_id` | 1:N |
| SI顧客 → 取引 | si_customers | si_transactions | `si_customer_id` | 1:N |
| PJ → 契約 | si_projects | si_contracts | `project_id` | 1:N |
| PJ ← エンジニア | si_projects | si_engineers | `project_id = current_project_id` | 1:N |

### クロスDB結合パターン

業務領域を横断した分析では、`domain_id_mappings` を経由して結合する。

```sql
-- 例: PREMIUMセグメントの携帯電話顧客の月間取引額
SELECT
  cs.segment_code,
  SUM(mt.amount) AS total_amount,
  COUNT(DISTINCT mc.mobile_customer_id) AS customer_count
FROM sqldb_common_01.customer_segments cs
JOIN sqldb_common_01.domain_id_mappings dm
  ON cs.unified_customer_id = dm.unified_customer_id
  AND dm.domain_code = 'MOBILE'
  AND dm.is_active = 1
JOIN sqldb_mobile_01.mobile_customers mc
  ON dm.domain_customer_id = mc.mobile_customer_id
JOIN sqldb_mobile_01.mobile_transactions mt
  ON mc.mobile_customer_id = mt.mobile_customer_id
WHERE cs.segment_code = 'PREMIUM'
  AND mt.transaction_date >= DATEADD(MONTH, -1, GETDATE())
GROUP BY cs.segment_code;
```

---

## メダリオン構成

### Bronze 層 — 生データ取り込み

| Lakehouse テーブル | ソース | 取り込み頻度 | 説明 |
|---|---|---|---|
| `bronze.raw_unified_customers` | sqldb_common_01.unified_customers | 日次 | 統合顧客マスタの全件スナップショット |
| `bronze.raw_domain_id_mappings` | sqldb_common_01.domain_id_mappings | 日次 | IDマッピングの全件スナップショット |
| `bronze.raw_customer_segments` | sqldb_common_01.customer_segments | 日次 | セグメントの全件スナップショット |
| `bronze.raw_mobile_customers` | sqldb_mobile_01.mobile_customers | 日次 | 携帯顧客マスタ |
| `bronze.raw_mobile_contracts` | sqldb_mobile_01.mobile_contracts | 日次 | 回線契約 |
| `bronze.raw_mobile_transactions` | sqldb_mobile_01.mobile_transactions | 時間次 | 携帯取引履歴（差分取り込み） |
| `bronze.raw_mobile_device_inventory` | sqldb_mobile_01.mobile_device_inventory | 日次 | 端末在庫 |
| `bronze.raw_sns_users` | sqldb_sns_01.sns_users | 日次 | SNSユーザーマスタ |
| `bronze.raw_sns_subscriptions` | sqldb_sns_01.sns_subscriptions | 日次 | SNSサブスクリプション |
| `bronze.raw_sns_transactions` | sqldb_sns_01.sns_transactions | 時間次 | SNS取引履歴（差分取り込み） |
| `bronze.raw_sns_ad_campaigns` | sqldb_sns_01.sns_ad_campaigns | 日次 | SNS広告キャンペーン |
| `bronze.raw_si_customers` | sqldb_si_01.si_customers | 日次 | SI顧客マスタ |
| `bronze.raw_si_projects` | sqldb_si_01.si_projects | 日次 | SIプロジェクト |
| `bronze.raw_si_contracts` | sqldb_si_01.si_contracts | 日次 | SI契約 |
| `bronze.raw_si_transactions` | sqldb_si_01.si_transactions | 時間次 | SI取引履歴（差分取り込み） |
| `bronze.raw_si_engineers` | sqldb_si_01.si_engineers | 日次 | SIエンジニアリソース |

### Silver 層 — クレンジング・正規化済み

| Lakehouse テーブル | 変換内容 | 説明 |
|---|---|---|
| `silver.customers_unified` | 名寄せ確認済み、NULL補完、住所正規化 | 統合顧客（クレンジング済み） |
| `silver.customer_domain_links` | is_active=1のみ、domain_code検証済み | 有効IDマッピング |
| `silver.customer_risk_profile` | セグメント＋リスクスコア正規化、外れ値除外 | 顧客リスクプロファイル |
| `silver.mobile_active_contracts` | status=ACTIVE、期間整合性チェック済み | 有効回線契約 |
| `silver.mobile_monthly_billing` | 月次集計、通貨統一、重複排除 | 携帯月次請求 |
| `silver.mobile_inventory_current` | 最新スナップショット、status正規化 | 端末在庫（最新） |
| `silver.sns_active_users` | status=ACTIVE、BOT除外、DAU算出可能 | SNSアクティブユーザー |
| `silver.sns_revenue_classified` | AD_REVENUE/SUBSCRIPTION/OTHER分類済み | SNS収益分類 |
| `silver.sns_campaign_performance` | 消化率・CTR算出済み | 広告キャンペーン実績 |
| `silver.si_project_portfolio` | ステータス正規化、工期整合性チェック | SIプロジェクトポートフォリオ |
| `silver.si_contract_obligations` | 契約種別分類、ペナルティ条項フラグ付与 | SI契約義務 |
| `silver.si_engineer_capacity` | スキルJSON展開、稼働状況正規化 | SIエンジニアキャパシティ |

### Gold 層 — 集計・KPIテーブル

| Lakehouse テーブル | 粒度 | 説明 |
|---|---|---|
| `gold.kpi_mobile_monthly` | 月次 | 携帯電話事業の月次KPI集計 |
| `gold.kpi_sns_monthly` | 月次 | SNS事業の月次KPI集計 |
| `gold.kpi_si_monthly` | 月次 | SI事業の月次KPI集計 |
| `gold.kpi_cross_domain_monthly` | 月次 | 全事業横断の月次KPI集計 |
| `gold.customer_360` | 顧客×時点 | 顧客360度ビュー（全領域統合） |
| `gold.impact_assessment_history` | イベント×評価 | インパクト評価履歴 |
| `gold.inventory_risk_daily` | 日次 | 端末在庫リスク日次サマリ |
| `gold.engineer_utilization_weekly` | 週次 | エンジニア稼働率週次サマリ |

---

## KPI定義

### 携帯電話事業 KPI（gold.kpi_mobile_monthly）

| KPI | 計算式 | 粒度 | スキル参照 |
|---|---|---|---|
| ARPU | `SUM(amount) / COUNT(DISTINCT mobile_customer_id)` WHERE transaction_type IN ('MONTHLY_FEE','VOICE','DATA') | 月次 | — |
| アクティブ契約数 | `COUNT(*)` WHERE status='ACTIVE' from mobile_contracts | 月次 | サイバー攻撃 |
| 在庫充足率 | `SUM(stock_quantity) / 月間販売予測` WHERE status='IN_STOCK' | 月次 | 半導体供給制約 |
| 在庫回転日数 | `AVG(stock_quantity) / (月間出荷数 / 30)` | 月次 | 半導体供給制約 |
| オンライン契約率 | オンライン新規 / 全新規契約 | 月次 | サイバー攻撃 |

### SNS事業 KPI（gold.kpi_sns_monthly）

| KPI | 計算式 | 粒度 | スキル参照 |
|---|---|---|---|
| DAU | `COUNT(DISTINCT sns_user_id)` WHERE last_login >= 対象日 | 日次→月次平均 | サイバー攻撃 |
| 広告収益 | `SUM(amount)` WHERE transaction_type='AD_REVENUE' | 月次 | 個人情報保護法, 半導体供給制約 |
| サブスク収益 | `SUM(monthly_fee)` WHERE status='ACTIVE' from sns_subscriptions | 月次 | 個人情報保護法 |
| 広告収益依存度 | 広告収益 / (広告収益 + サブスク収益) | 月次 | 個人情報保護法 |
| 広告主数 | `COUNT(DISTINCT advertiser_id)` WHERE status='ACTIVE' | 月次 | 個人情報保護法 |
| キャンペーン予算消化率 | `AVG(spent_amount / budget)` WHERE status='ACTIVE' | 月次 | — |

### SI事業 KPI（gold.kpi_si_monthly）

| KPI | 計算式 | 粒度 | スキル参照 |
|---|---|---|---|
| 進行中PJ数 | `COUNT(*)` WHERE status='IN_PROGRESS' | 月次 | 半導体供給制約 |
| 運用保守PJ数 | `COUNT(*)` WHERE status='MAINTENANCE' | 月次 | サイバー攻撃 |
| 受注残高 | `SUM(total_amount)` WHERE status='ACTIVE' from si_contracts | 月次 | 半導体供給制約 |
| エンジニア稼働率 | `COUNT(ASSIGNED) / COUNT(*)` from si_engineers | 月次 | 半導体供給制約, サイバー攻撃 |
| HW調達額 | `SUM(amount)` WHERE transaction_type='HW_PROCUREMENT' | 月次 | 半導体供給制約 |
| 平均PJ採算率 | `(contract_amount - 実コスト) / contract_amount` | PJ完了時 | 半導体供給制約 |

### クロスドメイン KPI（gold.kpi_cross_domain_monthly）

| KPI | 計算式 | 粒度 | スキル参照 |
|---|---|---|---|
| 総顧客数 | `COUNT(DISTINCT unified_customer_id)` WHERE is_active=1 | 月次 | 全スキル |
| PREMIUMセグメント比率 | `COUNT(PREMIUM) / COUNT(*)` from customer_segments | 月次 | 全スキル |
| マルチドメイン顧客率 | 2領域以上のマッピングを持つ顧客 / 全顧客 | 月次 | — |
| 平均リスクスコア | `AVG(risk_score)` from customer_segments | 月次 | — |

---

## データ品質・除外条件

- `updated_at` が NULL のレコードは初期ロードデータとして扱い、変更検知の対象外とする。
- 金額は JPY 換算値を優先する。`currency` が `JPY` 以外のレコードは補助情報として扱う。
- 個人を特定する粒度での出力は禁止する。出力はセグメントまたは集計単位にする。
- `domain_id_mappings.is_active = 0` のレコードは退会済みマッピングであり、現在の分析には含めない。
- `si_engineers.skill_set` が NULL または空文字のレコードはスキル未登録として、スキルベースの検索対象外とする。
- ニュース日当日のみではなく、前後期間を比較して一時ノイズを避ける。

## 用語・コード値

| 用語 | データ表現 | 補足 |
|---|---|---|
| PREMIUMセグメント | `segment_code = 'PREMIUM'` | 全スキルで優先対応対象 |
| ハイリスク顧客 | `risk_score >= 70` | 離脱リスクが高い顧客 |
| アクティブ契約 | `status = 'ACTIVE'` | 各テーブル共通 |
| 在庫あり | `status = 'IN_STOCK'` AND `stock_quantity > 0` | mobile_device_inventory |
| 広告収益 | `transaction_type = 'AD_REVENUE'` | sns_transactions |
| HW調達 | `transaction_type = 'HW_PROCUREMENT'` | si_transactions |
| インフラPJ | `project_type IN ('INFRASTRUCTURE','CLOUD_MIGRATION')` | si_projects |
| 即時対応可能 | `availability_status = 'AVAILABLE'` | si_engineers |
