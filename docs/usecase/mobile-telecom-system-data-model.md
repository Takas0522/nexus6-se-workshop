# モバイル通信業務システムデータモデル

関連シナリオ: [業務システム別ニュース分析シナリオ](../scenario/business-system-news-analysis-scenario.md)

## 対象業務システム
1. 顧客管理システム
2. 契約管理システム
3. 課金・請求システム
4. 在庫・端末管理システム
5. コールセンター/CRMシステム

## 主要データモデル
### 顧客マスタ
| 項目 | 型 | 説明 |
|---|---|---|
| customer_id | string | 顧客ID |
| customer_type | string | 個人/法人 |
| region | string | 居住地域 |
| age_band | string | 年齢帯 |
| segment | string | セグメント |

### 契約マスタ
| 項目 | 型 | 説明 |
|---|---|---|
| contract_id | string | 契約ID |
| customer_id | string | 顧客ID |
| plan_id | string | プランID |
| device_type | string | 端末種別 |
| update_month | string | 更新月 |
| subsidy_amount | decimal | 端末補助額 |

### 利用・請求データ
| 項目 | 型 | 説明 |
|---|---|---|
| usage_id | string | 利用明細ID |
| contract_id | string | 契約ID |
| usage_date | date | 利用日 |
| voice_usage | decimal | 音声利用量 |
| data_usage | decimal | データ利用量 |
| monthly_charge | decimal | 月額請求 |

### 端末・設備コストデータ
| 項目 | 型 | 説明 |
|---|---|---|
| cost_item_id | string | コストID |
| cost_type | string | 端末/基地局/保守 |
| currency | string | 通貨 |
| unit_cost | decimal | 単価 |
| procurement_date | date | 調達日 |
| vendor_region | string | 調達地域 |

### 問い合わせ・解約データ
| 項目 | 型 | 説明 |
|---|---|---|
| ticket_id | string | 問い合わせID |
| customer_id | string | 顧客ID |
| contact_reason | string | 問い合わせ理由 |
| created_at | datetime | 発生日時 |
| resolved_at | datetime | 解決日時 |
| cancel_flag | boolean | 解約有無 |

## 補助データモデル
### プラン・料金ルール
| 項目 | 型 | 説明 |
|---|---|---|
| plan_rule_id | string | ルールID |
| plan_id | string | プランID |
| base_fee | decimal | 基本料金 |
| overage_rule | string | 追加課金ルール |
| device_discount_rule | string | 端末割引ルール |
| valid_from / valid_to | datetime | 有効期間 |

### 端末SKUマスタ
| 項目 | 型 | 説明 |
|---|---|---|
| device_sku_id | string | 端末SKU |
| device_name | string | 端末名 |
| supplier_region | string | 仕入地域 |
| import_currency | string | 仕入通貨 |
| standard_cost | decimal | 標準原価 |
| sales_price | decimal | 販売価格 |

### 施策配信・反応履歴
| 項目 | 型 | 説明 |
|---|---|---|
| action_id | string | 施策ID |
| customer_id | string | 顧客ID |
| action_type | string | 通知/割引/保留/提案 |
| sent_at | datetime | 配信日時 |
| response_type | string | 開封/申込/無反応/解約抑止 |
| response_at | datetime | 反応日時 |

### 外部依存：顧客統合ID基盤
顧客IDの名寄せと統合管理は社内共通システムに委譲する。  
→ 参照: [顧客統合ID基盤（共通システム）](common-customer-identity-system.md)

本事業部の `customer_id` は `ドメイン別IDマッピング`（domain=`mobile`）で `unified_customer_id` と紐付く。

## 機能補完データモデル
_対象業務システムの各機能を満たすために必要なモデル_

### ネットワーク基地局マスタ（在庫・端末管理システム）
| 項目 | 型 | 説明 |
|---|---|---|
| station_id | string | 基地局ID |
| region | string | 設置地域 |
| equipment_type | string | 設備種別（4G/5G/光回線） |
| vendor_name | string | ベンダー名 |
| vendor_country | string | ベンダー所在国 |
| contract_currency | string | 保守契約通貨 |
| install_date | date | 設置日 |
| maintenance_cost | decimal | 年間保守費用 |

### MNP（番号ポータビリティ）履歴（契約管理システム）
| 項目 | 型 | 説明 |
|---|---|---|
| mnp_id | string | MNP転出入ID |
| customer_id | string | 顧客ID |
| mnp_type | string | 転入/転出 |
| from_carrier | string | 転出元キャリア |
| to_carrier | string | 転入先キャリア |
| executed_at | datetime | 実施日時 |
| trigger_reason | string | 転出理由（競合キャンペーン/料金/品質） |

### 端末分割払い明細（課金・請求システム）
| 項目 | 型 | 説明 |
|---|---|---|
| installment_id | string | 分割払いID |
| contract_id | string | 契約ID |
| device_sku_id | string | 端末SKU |
| total_amount | decimal | 分割総額 |
| monthly_payment | decimal | 月額支払い |
| remaining_months | integer | 残回数 |
| interest_rate | decimal | 実質年率 |
| start_date | date | 開始日 |

### オプション・追加サービスマスタ（契約管理システム）
| 項目 | 型 | 説明 |
|---|---|---|
| option_id | string | オプションID |
| option_name | string | オプション名 |
| option_fee | decimal | 月額料金 |
| option_category | string | 保険/エンタメ/セキュリティ |
| valid_from / valid_to | datetime | 提供期間 |

### 顧客オプション契約（契約管理システム）
| 項目 | 型 | 説明 |
|---|---|---|
| customer_option_id | string | 顧客オプションID |
| customer_id | string | 顧客ID |
| option_id | string | オプションID |
| subscribed_at | datetime | 加入日時 |
| cancelled_at | datetime | 解約日時（NULL=継続中） |

## 業務システム別データモデルマッピング

| 業務システム | 使用するデータモデル |
|---|---|
| 顧客管理システム | 顧客マスタ、IDマッピング |
| 契約管理システム | 契約マスタ、MNP（番号ポータビリティ）履歴、オプション・追加サービスマスタ、顧客オプション契約 |
| 課金・請求システム | 利用・請求データ、プラン・料金ルール、端末分割払い明細 |
| 在庫・端末管理システム | 端末・設備コストデータ、端末SKUマスタ、ネットワーク基地局マスタ |
| コールセンター/CRMシステム | 問い合わせ・解約データ、施策配信・反応履歴 |

## ニュースから得たい内部インサイト
- 為替急変: 輸入コスト上昇が端末粗利と施策原資に与える影響
- 競合統合: 料金プラン、乗換率、解約予兆への影響
- 金融政策転換: 分割購入や設備投資のコスト影響
