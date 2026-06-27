# 07. Microsoft Fabric データモデル設計

関連: [開発計画 README](README.md) | [データ取り込み設計](06-fabric-data-ingestion.md)

---

## Demo と本番のスコープ

本設計書は**本番データモデルの全容を記述**し、Demo ではその一部を合成 CSV で作成する。

| 区分 | 対象テーブル | 生成手段 |
|---|---|---|
| **Demo 必須** | `common.unified_customers` / `mobile.contracts` / `mobile.mnp_history` / `mobile.device_costs` / `ecommerce.orders` / `ecommerce.inventory` / `ecommerce.campaign_reactions` / `fintech.fx_positions` / `fintech.loan_balances` / `fintech.credit_reviews` / Gold `kpi.*` ・ `*_ai.risk_summary` | `scripts/seed-data/generate_csv.py` で合成 CSV を生成し Bronze に配置、Notebook 2 本で Silver/Gold を作成 |
| **Demo 対象外（本番設計のみ）** | `mobile.usage_billing` / `mobile.installment_details` / `mobile.crm_tickets` / `ecommerce.members` / `ecommerce.member_behaviors` / `ecommerce.point_events` / `fintech.accounts` / `fintech.card_transactions` / `fintech.fx_rate_snapshots` / `common.domain_id_mappings` / `common.customer_segments` | 本設計書にスキーマのみ記載。Demo では作成しない |
| **usecase 完全版との差分** | `mobile.customers` （customer_type=個人/法人を含む顧客マスタ）、収益・リスク詳細、リスクイベント履歴、取引アラート、商品・カテゴリマスタ、出店者、価格ルール、返品履歴 など | `docs/usecase/*` で定義されるが、本 Fabric モデルでは Demo / 本番とも取り込み対象外として割愛 |

> Demo 出力は Gold (`kpi.monthly_revenue`, `kpi.monthly_cost_detail`, `kpi.customer_count`, `kpi.fx_sensitivity`, `mobile_ai.risk_summary`, `ecommerce_ai.risk_summary`, `fintech_ai.risk_summary`) の 7 テーブルがあれば Agent 2/3 は動作する。

---

## 規模前提・レコード数試算

グループ全体 **30,000 名**・**過去 6 ヶ月**を前提とし、各事業部の利用状況から推定する。

| 事業部 | 想定顧客数 | 備考 |
|---|---|---|
| モバイル | 30,000 名 | 全員が何らかの契約を保有 |
| Eコマース | 20,000 名 | モバイル利用者のうち 67% が会員 |
| Fintech | 15,000 名 | モバイル利用者のうち 50% が口座保有 |
| 共通 ID | 30,000 名 | 全員 unified_customer_id を持つ |

---

## 共通設計ルール

| 項目 | ルール |
|---|---|
| テーブル形式 | Delta Table（Parquet ベース） |
| パーティションキー | `year_month` (string, yyyy-MM) |
| ソートキー | 主キー降順 |
| タイムスタンプ | すべて UTC で格納、JST 変換は Gold ビューで実施 |
| 主キー型 | string（ULID 推奨。ソース側の ID 体系をそのまま保持） |
| 削除フラグ | 物理削除なし。`is_deleted` (boolean) + `deleted_at` で論理削除 |
| 付加メタ列 | `_ingest_date`（取り込み日）、`_source_system`（ソースシステム名） |

---

## 共通（顧客統合 ID 基盤）

### Silver: `common.unified_customers`

| カラム名 | 型 | 説明 | 備考 |
|---|---|---|---|
| unified_customer_id | string | 統合顧客ID（PK） | ULID |
| full_name | string | 氏名 | |
| birth_date | date | 生年月日 | |
| gender | string | 性別 | M/F/Other |
| region | string | 居住地域 | 都道府県コード |
| age_band | string | 年齢帯 | 10s/20s/30s/40s/50s/60s+ |
| primary_email | string | 代表メール | |
| primary_phone | string | 代表電話 | マスク済み |
| kyc_status | string | 本人確認ステータス | verified/pending/rejected |
| registered_at | timestamp | 初回登録日時 | UTC |
| last_updated_at | timestamp | 最終更新日時 | UTC |
| is_deleted | boolean | 論理削除フラグ | |
| _ingest_date | date | 取り込み日 | |

**推定レコード数**: 30,000 行（マスタ）

---

### Silver: `common.domain_id_mappings`

| カラム名 | 型 | 説明 |
|---|---|---|
| map_id | string | マッピングID (PK) |
| unified_customer_id | string | 統合顧客ID |
| domain | string | mobile / ecommerce / fintech |
| domain_customer_id | string | 各事業部の顧客ID |
| source_system | string | 元業務システム名 |
| linked_at | timestamp | 紐付け日時 |
| link_status | string | 有効/無効/審査中 |
| _ingest_date | date | |

**推定レコード数**: 約 65,000 行（1顧客あたり平均 2.1 ドメイン紐付け）

---

### Silver: `common.customer_segments`

| カラム名 | 型 | 説明 |
|---|---|---|
| assignment_id | string | 割り当てID (PK) |
| unified_customer_id | string | 統合顧客ID |
| segment_id | string | セグメントID |
| segment_name | string | セグメント名 |
| assigned_at | timestamp | 割り当て日時 |
| expires_at | timestamp | 有効期限（NULL=無期限） |
| _ingest_date | date | |

**推定レコード数**: 約 60,000 行（1顧客あたり平均 2 セグメント）

---

## モバイル通信

### Silver: `mobile.contracts`（契約マスタ）

| カラム名 | 型 | 説明 |
|---|---|---|
| contract_id | string | 契約ID (PK) |
| customer_id | string | 顧客ID |
| plan_id | string | プランID |
| device_type | string | 端末種別 |
| update_month | string | 更新月 (yyyy-MM) |
| subsidy_amount | decimal(12,2) | 端末補助額 (JPY) |
| contract_start_date | date | 契約開始日 |
| contract_end_date | date | 契約終了日 |
| status | string | active/cancelled/suspended |
| is_deleted | boolean | 論理削除フラグ |
| year_month | string | パーティションキー |
| _ingest_date | date | |

**推定レコード数**: 約 35,000 行（現在有効 + 6 ヶ月内解約分）

---

### Silver: `mobile.usage_billing`（利用・請求データ）

| カラム名 | 型 | 説明 |
|---|---|---|
| usage_id | string | 利用明細ID (PK) |
| contract_id | string | 契約ID |
| customer_id | string | 顧客ID |
| usage_date | date | 利用日 |
| voice_usage_min | decimal(10,2) | 音声利用量（分） |
| data_usage_gb | decimal(10,3) | データ利用量（GB） |
| monthly_charge_jpy | decimal(12,2) | 月額請求（円） |
| overage_charge_jpy | decimal(12,2) | 超過料金（円） |
| year_month | string | パーティションキー |
| _ingest_date | date | |

**推定レコード数**: 約 210,000 行（平均有効契約 35,000 × 6 ヶ月の月次請求）

---

### Silver: `mobile.device_costs`（端末・設備コスト）

| カラム名 | 型 | 説明 |
|---|---|---|
| cost_item_id | string | コストID (PK) |
| cost_type | string | 端末/基地局/保守 |
| currency | string | 通貨コード (ISO 4217) |
| unit_cost | decimal(14,2) | 単価（原通貨） |
| unit_cost_jpy | decimal(14,2) | 単価（円換算） |
| fx_rate_used | decimal(10,4) | 適用為替レート |
| procurement_date | date | 調達日 |
| vendor_region | string | 調達地域 |
| year_month | string | パーティションキー |
| _ingest_date | date | |

**推定レコード数**: 約 3,600 行（月 600 件 × 6 ヶ月）

---

### Silver: `mobile.mnp_history`（MNP 履歴）

| カラム名 | 型 | 説明 |
|---|---|---|
| mnp_id | string | MNP 転出入ID (PK) |
| customer_id | string | 顧客ID |
| mnp_type | string | 転入/転出 |
| from_carrier | string | 転出元キャリア |
| to_carrier | string | 転入先キャリア |
| executed_at | timestamp | 実施日時 |
| trigger_reason | string | 競合キャンペーン/料金/品質 |
| year_month | string | パーティションキー |
| _ingest_date | date | |

**推定レコード数**: 約 9,000 行（月 1,500 件 × 6 ヶ月 ※転出入合計）

---

### Silver: `mobile.installment_details`（端末分割払い明細）

| カラム名 | 型 | 説明 |
|---|---|---|
| installment_id | string | 分割払いID (PK) |
| contract_id | string | 契約ID |
| device_sku_id | string | 端末SKU |
| total_amount_jpy | decimal(12,2) | 分割総額（円） |
| monthly_payment_jpy | decimal(12,2) | 月額支払い（円） |
| remaining_months | integer | 残回数 |
| interest_rate | decimal(6,4) | 実質年率 |
| start_date | date | 開始日 |
| year_month | string | パーティションキー |
| _ingest_date | date | |

**推定レコード数**: 約 45,000 行（端末購入者の分割払い月次スナップショット）

> スキーマは「月次スナップショット（同一 `installment_id` が `year_month` ごとに 1 行）」を前提とする。請求生成は Silver では行わず、Gold で集計する。

---

### Silver: `mobile.crm_tickets`（問い合わせ・解約データ）

| カラム名 | 型 | 説明 |
|---|---|---|
| ticket_id | string | 問い合わせID (PK) |
| customer_id | string | 顧客ID |
| contact_reason | string | 問い合わせ理由 |
| created_at | timestamp | 発生日時 |
| resolved_at | timestamp | 解決日時 |
| cancel_flag | boolean | 解約有無 |
| resolution_days | integer | 解決日数（計算列） |
| year_month | string | パーティションキー |
| _ingest_date | date | |

**推定レコード数**: 約 18,000 行（月 3,000 件 × 6 ヶ月）

---

## Eコマース

### Silver: `ecommerce.members`（会員マスタ）

| カラム名 | 型 | 説明 |
|---|---|---|
| member_id | string | 会員ID (PK) |
| member_name | string | 会員名 |
| email | string | メールアドレス（マスク済み） |
| rank | string | 会員ランク（Bronze/Silver/Gold/Platinum） |
| registered_at | date | 登録日 |
| region | string | 居住地域 |
| age_band | string | 年齢帯 |
| is_deleted | boolean | 論理削除 |
| _ingest_date | date | |

**推定レコード数**: 約 20,000 行

---

### Silver: `ecommerce.orders`（受注データ）

| カラム名 | 型 | 説明 |
|---|---|---|
| order_id | string | 受注ID (PK) |
| member_id | string | 会員ID |
| sku | string | SKU |
| order_date | date | 受注日 |
| quantity | integer | 数量 |
| order_amount_jpy | decimal(12,2) | 受注金額（円） |
| procurement_currency | string | 仕入通貨 |
| cost_price_jpy | decimal(12,2) | 原価（円換算） |
| gross_margin_jpy | decimal(12,2) | 粗利（計算列） |
| fx_rate_used | decimal(10,4) | 適用為替レート |
| year_month | string | パーティションキー |
| _ingest_date | date | |

**推定レコード数**: 約 540,000 行（20,000 会員 × 月 4.5 件 × 6 ヶ月）

---

### Silver: `ecommerce.inventory`（在庫データ）

| カラム名 | 型 | 説明 |
|---|---|---|
| inventory_id | string | 在庫ID (PK) |
| sku | string | SKU |
| warehouse_id | string | 倉庫ID |
| stock_qty | integer | 在庫数 |
| arrival_date | date | 入荷日 |
| import_currency | string | 輸入通貨 |
| import_cost_jpy | decimal(12,2) | 輸入コスト（円換算） |
| snapshot_date | date | スナップショット日（日次） |
| year_month | string | パーティションキー |
| _ingest_date | date | |

**推定レコード数**: 約 900,000 行（SKU 5,000 × 倉庫 10 × 6 ヶ月 × 月次スナップ）

---

### Silver: `ecommerce.point_events`（ポイント付与・利用履歴）

| カラム名 | 型 | 説明 |
|---|---|---|
| point_event_id | string | ポイントイベントID (PK) |
| member_id | string | 会員ID |
| event_type | string | 付与/利用/失効/調整 |
| point_amount | integer | ポイント数 |
| balance_after | integer | イベント後残高 |
| related_order_id | string | 関連受注ID |
| event_at | timestamp | 発生日時 |
| expiry_at | timestamp | 有効期限 |
| year_month | string | パーティションキー |
| _ingest_date | date | |

**推定レコード数**: 約 720,000 行（受注 540,000 + 利用・失効等）

---

### Silver: `ecommerce.member_behaviors`（会員行動ログ）

| カラム名 | 型 | 説明 |
|---|---|---|
| event_id | string | 行動ID (PK) |
| member_id | string | 会員ID |
| event_type | string | 閲覧/カート/購入/離脱 |
| event_time | timestamp | 発生日時 |
| device_type | string | PC/Mobile/Tablet |
| sku | string | 対象SKU（閲覧・カートのみ） |
| session_id | string | セッションID |
| year_month | string | パーティションキー |
| _ingest_date | date | |

**推定レコード数**: 約 7,200,000 行（20,000 会員 × 月 60 行動 × 6 ヶ月）

> ⚠️ 大容量テーブルのため Gold では集計済みサマリのみを AI エージェントに提供する。

---

### Silver: `ecommerce.campaign_reactions`（キャンペーン反応履歴）

| カラム名 | 型 | 説明 |
|---|---|---|
| reaction_id | string | 反応ID (PK) |
| campaign_id | string | キャンペーンID |
| member_id | string | 会員ID |
| reaction_type | string | クリック/購入/離脱/再訪 |
| reaction_time | timestamp | 反応日時 |
| point_rate | decimal(6,4) | 適用ポイント還元率 |
| year_month | string | パーティションキー |
| _ingest_date | date | |

**推定レコード数**: 約 480,000 行（月 80,000 件 × 6 ヶ月）

---

## Fintech

### Silver: `fintech.accounts`（顧客口座マスタ）

| カラム名 | 型 | 説明 |
|---|---|---|
| account_id | string | 口座ID (PK) |
| user_id | string | 顧客ID |
| account_type | string | 普通/投資/外貨/証券 |
| opened_at | date | 開設日 |
| balance_jpy | decimal(16,2) | 残高（円換算） |
| balance_currency | string | 原通貨 |
| balance_original | decimal(16,2) | 原通貨残高 |
| kyc_status | string | verified/pending/rejected |
| is_deleted | boolean | 論理削除 |
| _ingest_date | date | |

**推定レコード数**: 約 22,500 行（15,000 名 × 平均 1.5 口座）

---

### Silver: `fintech.card_transactions`（カード・決済データ）

| カラム名 | 型 | 説明 |
|---|---|---|
| transaction_id | string | 取引ID (PK) |
| card_id | string | カードID |
| user_id | string | 顧客ID |
| merchant_id | string | 加盟店ID |
| transaction_at | timestamp | 取引日時 |
| amount_jpy | decimal(12,2) | 金額（円換算） |
| amount_original | decimal(12,2) | 原通貨金額 |
| transaction_currency | string | 取引通貨 |
| overseas_flag | boolean | 海外決済有無 |
| fx_rate_used | decimal(10,4) | 適用為替レート |
| year_month | string | パーティションキー |
| _ingest_date | date | |

**推定レコード数**: 約 5,400,000 行（15,000 名 × 月 60 件 × 6 ヶ月）

---

### Silver: `fintech.fx_positions`（FX・証券・暗号資産データ）

| カラム名 | 型 | 説明 |
|---|---|---|
| position_id | string | ポジションID (PK) |
| user_id | string | 顧客ID |
| product_type | string | FX/証券/暗号資産 |
| position_amount | decimal(18,6) | 建玉・保有量 |
| position_currency | string | ポジション通貨 |
| pnl_jpy | decimal(14,2) | 損益（円換算） |
| market_currency | string | 取引通貨 |
| snapshot_date | date | スナップショット日 |
| year_month | string | パーティションキー |
| _ingest_date | date | |

**推定レコード数**: 約 180,000 行（月次スナップ × 6）

---

### Silver: `fintech.loan_balances`（ローン・リボ払い残高）

| カラム名 | 型 | 説明 |
|---|---|---|
| loan_id | string | ローンID (PK) |
| user_id | string | 顧客ID |
| loan_type | string | 住宅ローン/カードローン/リボ払い |
| principal_balance_jpy | decimal(14,2) | 元本残高（円） |
| interest_rate | decimal(6,4) | 適用金利 |
| monthly_payment_jpy | decimal(12,2) | 月額返済額（円） |
| maturity_date | date | 満期日 |
| overdue_flag | boolean | 延滞フラグ |
| snapshot_date | date | スナップショット日 |
| year_month | string | パーティションキー |
| _ingest_date | date | |

**推定レコード数**: 約 30,000 行（5,000 件 × 6 ヶ月スナップ）

---

### Silver: `fintech.fx_rate_snapshots`（為替レートスナップショット）

| カラム名 | 型 | 説明 |
|---|---|---|
| rate_snapshot_id | string | スナップショットID (PK) |
| base_currency | string | 基準通貨 |
| quote_currency | string | 対象通貨 |
| mid_rate | decimal(12,6) | 仲値レート |
| bid_rate | decimal(12,6) | 買値 |
| ask_rate | decimal(12,6) | 売値 |
| captured_at | timestamp | 取得日時 |
| source | string | レートソース |
| year_month | string | パーティションキー |
| _ingest_date | date | |

**推定レコード数**: 約 10,800 行（主要 10 通貨ペア × 日 6 回 × 180 日）

> このテーブルは Silver に格納し、他テーブルの JPY 換算計算に参照される。

---

### Silver: `fintech.credit_reviews`（与信・審査データ）

| カラム名 | 型 | 説明 |
|---|---|---|
| review_id | string | 審査ID (PK) |
| user_id | string | 顧客ID |
| credit_score | decimal(6,2) | 与信スコア |
| approval_status | string | 承認/否認/保留 |
| review_reason | string | 判定理由 |
| reviewed_at | timestamp | 審査日時 |
| year_month | string | パーティションキー |
| _ingest_date | date | |

**推定レコード数**: 約 18,000 行（月 3,000 件 × 6 ヶ月）

---

## Gold テーブル設計（AI エージェント向け）

### `kpi.monthly_revenue`（Agent 2 向け）

| カラム名 | 型 | 説明 |
|---|---|---|
| year_month | string | 年月 (PK1) |
| division | string | mobile / ecommerce / fintech (PK2) |
| gross_revenue_jpy | decimal(16,2) | 売上総額 |
| total_cost_jpy | decimal(16,2) | 総コスト |
| gross_margin_jpy | decimal(16,2) | 粗利 |
| gross_margin_rate | decimal(6,4) | 粗利率 |
| fx_exposure_usd | decimal(14,2) | USD 為替エクスポージャー |
| fx_exposure_other_jpy | decimal(14,2) | その他外貨エクスポージャー（円換算） |
| active_customer_count | integer | 有効顧客数 |
| churned_customer_count | integer | 解約顧客数 |

---

### `mobile_ai.risk_summary`（Agent 3 Mobile 向け）

| カラム名 | 型 | 説明 |
|---|---|---|
| year_month | string | 年月 |
| metric_name | string | 指標名 |
| metric_value | decimal(16,4) | 指標値 |
| metric_unit | string | 単位（JPY / 件 / % 等） |
| description | string | 指標説明 |

集計する主要指標:
- 海外仕入コスト合計（月次）
- 端末補助額合計 / 粗利圧縮額
- MNP 転出件数・転出率
- 分割払い残高合計・平均金利
- 解約問い合わせ件数・解約率

---

### `ecommerce_ai.risk_summary`（Agent 3 EC 向け）

集計する主要指標:
- 越境 EC 仕入コスト（通貨別）
- 粗利率の月次推移（カテゴリ別）
- ポイント還元コスト合計
- 会員行動: カート離脱率 / 再訪率
- キャンペーン ROI（reaction / budget）

---

### `fintech_ai.risk_summary`（Agent 3 Fintech 向け）

集計する主要指標:
- FX ポジション損益合計（通貨別）
- 海外カード決済額・為替差損
- ローン残高合計・延滞率
- リボ払い残高と平均金利
- 与信審査：否認率・保留率

---

## レコード数サマリ

| Lakehouse | テーブル | 推定レコード数 | Demo 対象 |
|---|---|---|---|
| Silver | common.unified_customers | 30,000 | ○ |
| Silver | common.domain_id_mappings | 65,000 | × |
| Silver | common.customer_segments | 60,000 | × |
| Silver | mobile.contracts | 35,000 | ○ |
| Silver | mobile.usage_billing | 210,000 | × |
| Silver | mobile.mnp_history | 9,000 | ○ |
| Silver | mobile.device_costs | 3,600 | ○ |
| Silver | mobile.installment_details | 45,000 | × |
| Silver | mobile.crm_tickets | 18,000 | × |
| Silver | ecommerce.members | 20,000 | × |
| Silver | ecommerce.orders | 540,000 | ○ |
| Silver | ecommerce.inventory | 900,000 | ○ |
| Silver | ecommerce.member_behaviors | 7,200,000 | × |
| Silver | ecommerce.point_events | 720,000 | × |
| Silver | ecommerce.campaign_reactions | 480,000 | ○ |
| Silver | fintech.accounts | 22,500 | × |
| Silver | fintech.card_transactions | 5,400,000 | × |
| Silver | fintech.fx_positions | 180,000 | ○ |
| Silver | fintech.loan_balances | 30,000 | ○ |
| Silver | fintech.fx_rate_snapshots | 10,800 | × |
| Silver | fintech.credit_reviews | 18,000 | ○ |
| **本番合計** | | **≒ 15,996,900 行** | |
| **Demo 合計** | | **≒ 2,237,600 行**（うち ecommerce.inventory 900,000 / orders 540,000） | |

> Gold テーブルは Silver を集計したサマリで、行数は各テーブル数十〜数千行程度。
> Demo ではさらに規模を縮小しても良い（例: `ecommerce.orders` 50,000 行 / `ecommerce.inventory` 10,000 行など）。`scripts/seed-data/generate_csv.py` のパラメータで調整する。
