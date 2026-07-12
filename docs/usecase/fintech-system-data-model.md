# Fintech業務システムデータモデル

関連シナリオ: [業務システム別ニュース分析シナリオ](../scenario/business-system-news-analysis-scenario.md)

## 対象業務システム
1. 口座管理システム
2. カード管理システム
3. 決済ゲートウェイ
4. FX/証券/暗号資産取引システム
5. 与信・審査システム

## 主要データモデル
### 顧客口座マスタ
| 項目 | 型 | 説明 |
|---|---|---|
| user_id | string | 顧客ID |
| account_id | string | 口座ID |
| account_type | string | 普通/投資/外貨など |
| opened_at | date | 開設日 |
| balance | decimal | 残高 |

### カード・決済データ
| 項目 | 型 | 説明 |
|---|---|---|
| card_id | string | カードID |
| user_id | string | 顧客ID |
| transaction_id | string | 取引ID |
| transaction_date | datetime | 取引日時 |
| amount | decimal | 金額 |
| overseas_flag | boolean | 海外決済有無 |

### FX・証券・暗号資産データ
| 項目 | 型 | 説明 |
|---|---|---|
| position_id | string | ポジションID |
| user_id | string | 顧客ID |
| product_type | string | FX/証券/暗号資産 |
| position_amount | decimal | 建玉/保有量 |
| pnl_amount | decimal | 損益 |
| market_currency | string | 取引通貨 |

### 与信・審査データ
| 項目 | 型 | 説明 |
|---|---|---|
| review_id | string | 審査ID |
| user_id | string | 顧客ID |
| credit_score | decimal | 与信スコア |
| approval_status | string | 承認/否認/保留 |
| review_reason | string | 判定理由 |
| reviewed_at | datetime | 審査日時 |

### 収益・リスクデータ
| 項目 | 型 | 説明 |
|---|---|---|
| revenue_id | string | 収益ID |
| business_line | string | 事業ライン |
| fee_revenue | decimal | 手数料収益 |
| interest_revenue | decimal | 金利収益 |
| fx_revenue | decimal | 為替収益 |
| risk_loss | decimal | リスク損失 |

## 補助データモデル
### 金利・商品ルール
| 項目 | 型 | 説明 |
|---|---|---|
| product_rule_id | string | ルールID |
| product_type | string | 商品種別 |
| interest_rate | decimal | 金利 |
| fee_rate | decimal | 手数料率 |
| leverage_limit | decimal | レバレッジ上限 |
| valid_from / valid_to | datetime | 有効期間 |

### リスクイベント履歴
| 項目 | 型 | 説明 |
|---|---|---|
| risk_event_id | string | イベントID |
| user_id | string | 顧客ID |
| risk_type | string | 不正/延滞/市場/流動性 |
| risk_score | decimal | リスクスコア |
| detected_at | datetime | 検知日時 |

### 顧客・口座紐付け履歴
| 項目 | 型 | 説明 |
|---|---|---|
| link_id | string | 紐付けID |
| user_id | string | 顧客ID |
| external_account_ref | string | 外部口座参照 |
| link_status | string | 有効/無効 |
| linked_at | datetime | 紐付け日時 |

### 外部依存：顧客統合ID基盤
顧客IDの名寄せと統合管理は社内共通システムに委譲する。  
→ 参照: [顧客統合ID基盤（共通システム）](common-customer-identity-system.md)

本事業部の `user_id` は `ドメイン別IDマッピング`（domain=`fintech`）で `unified_customer_id` と紐付く。  
また、FintechのKYC記録は統合顧客マスタの `kyc_status` に反映され、全事業部が参照できる。

## 機能補完データモデル
_対象業務システムの各機能を満たすために必要なモデル_

### ローン・リボ払い残高データ（与信・審査システム）
| 項目 | 型 | 説明 |
|---|---|---|
| loan_id | string | ローンID |
| user_id | string | 顧客ID |
| loan_type | string | 住宅ローン/カードローン/リボ払い |
| principal_balance | decimal | 元本残高 |
| interest_rate | decimal | 適用金利 |
| monthly_payment | decimal | 月額返済額 |
| maturity_date | date | 満期日 |
| overdue_flag | boolean | 延滞フラグ |

### 加盟店マスタ（決済ゲートウェイ）
| 項目 | 型 | 説明 |
|---|---|---|
| merchant_id | string | 加盟店ID |
| merchant_name | string | 加盟店名 |
| merchant_category | string | 業種コード（MCC） |
| settlement_currency | string | 決済通貨 |
| fee_rate | decimal | 加盟店手数料率 |
| overseas_flag | boolean | 海外加盟店有無 |
| contracted_at | date | 契約開始日 |

### 為替レートスナップショット（FX/証券/暗号資産取引システム）
| 項目 | 型 | 説明 |
|---|---|---|
| rate_snapshot_id | string | スナップショットID |
| base_currency | string | 基準通貨 |
| quote_currency | string | 対象通貨 |
| mid_rate | decimal | 仲値レート |
| bid_rate | decimal | 買値 |
| ask_rate | decimal | 売値 |
| captured_at | datetime | 取得日時 |
| source | string | レートソース |

### KYC・本人確認記録（口座管理システム）
| 項目 | 型 | 説明 |
|---|---|---|
| kyc_id | string | KYC記録ID |
| user_id | string | 顧客ID |
| kyc_status | string | 完了/審査中/否認 |
| id_type | string | 証明書種別 |
| verified_at | datetime | 確認完了日時 |
| expiry_at | datetime | 有効期限 |
| review_result | string | 審査結果 |

### 取引アラート・通知履歴（決済ゲートウェイ / 与信・審査システム）
| 項目 | 型 | 説明 |
|---|---|---|
| alert_id | string | アラートID |
| user_id | string | 顧客ID |
| alert_type | string | 不正疑義/延滞/限度超過/本人確認 |
| alert_level | string | 低/中/高/緊急 |
| triggered_at | datetime | 発生日時 |
| notified_at | datetime | 通知日時 |
| resolved_at | datetime | 解消日時（NULL=未解消） |

## 業務システム別データモデルマッピング

| 業務システム | 使用するデータモデル |
|---|---|
| 口座管理システム | 顧客口座マスタ、KYC・本人確認記録、顧客・口座紐付け履歴、IDマッピング |
| カード管理システム | カード・決済データ、取引アラート・通知履歴 |
| 決済ゲートウェイ | カード・決済データ、加盟店マスタ、取引アラート・通知履歴 |
| FX/証券/暗号資産取引システム | FX・証券・暗号資産データ、為替レートスナップショット、金利・商品ルール |
| 与信・審査システム | 与信・審査データ、ローン・リボ払い残高データ、リスクイベント履歴、収益・リスクデータ |

## ニュースから得たい内部インサイト
- 為替急変: FX/海外決済/暗号資産の収益・リスク
- 競合統合: 決済・カードシェアと利用継続率
- 金融政策転換: 金利収益、住宅ローン、リボ、与信コスト
