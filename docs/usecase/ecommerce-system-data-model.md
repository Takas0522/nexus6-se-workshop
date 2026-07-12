# Eコマース業務システムデータモデル

関連シナリオ: [業務システム別ニュース分析シナリオ](../scenario/business-system-news-analysis-scenario.md)

## 対象業務システム
1. 商品管理システム
2. 受注管理システム
3. 在庫管理システム
4. 会員・ポイント管理システム
5. マーケティング/キャンペーン管理システム

## 主要データモデル
### 商品マスタ
| 項目 | 型 | 説明 |
|---|---|---|
| sku | string | 商品SKU |
| product_name | string | 商品名 |
| category | string | カテゴリ |
| procurement_currency | string | 仕入通貨 |
| cost_price | decimal | 原価 |
| selling_price | decimal | 販売価格 |

### 受注データ
| 項目 | 型 | 説明 |
|---|---|---|
| order_id | string | 受注ID |
| member_id | string | 会員ID |
| sku | string | SKU |
| order_date | date | 受注日 |
| quantity | integer | 数量 |
| order_amount | decimal | 受注金額 |

### 在庫データ
| 項目 | 型 | 説明 |
|---|---|---|
| inventory_id | string | 在庫ID |
| sku | string | SKU |
| warehouse_id | string | 倉庫ID |
| stock_qty | integer | 在庫数 |
| arrival_date | date | 入荷日 |
| import_currency | string | 輸入通貨 |

### ポイント・キャンペーンデータ
| 項目 | 型 | 説明 |
|---|---|---|
| campaign_id | string | キャンペーンID |
| member_id | string | 会員ID |
| point_rate | decimal | ポイント還元率 |
| campaign_budget | decimal | 予算 |
| campaign_start_date | date | 開始日 |
| campaign_end_date | date | 終了日 |

### 会員行動データ
| 項目 | 型 | 説明 |
|---|---|---|
| event_id | string | 行動ID |
| member_id | string | 会員ID |
| event_type | string | 閲覧/カート/購入/離脱 |
| event_time | datetime | 発生日時 |
| device_type | string | デバイス種別 |

## 補助データモデル
### 価格ルール
| 項目 | 型 | 説明 |
|---|---|---|
| price_rule_id | string | ルールID |
| sku | string | SKU |
| min_price | decimal | 最低価格 |
| max_price | decimal | 最高価格 |
| markdown_rule | string | 値引きルール |
| valid_from / valid_to | datetime | 有効期間 |

### 配送・物流ルート
| 項目 | 型 | 説明 |
|---|---|---|
| shipping_route_id | string | ルートID |
| warehouse_id | string | 倉庫ID |
| destination_region | string | 配送先地域 |
| lead_time_days | integer | リードタイム |
| shipping_cost | decimal | 配送コスト |

### キャンペーン反応履歴
| 項目 | 型 | 説明 |
|---|---|---|
| reaction_id | string | 反応ID |
| campaign_id | string | キャンペーンID |
| member_id | string | 会員ID |
| reaction_type | string | クリック/購入/離脱/再訪 |
| reaction_time | datetime | 反応日時 |

### 外部依存：顧客統合ID基盤
顧客IDの名寄せと統合管理は社内共通システムに委譲する。  
→ 参照: [顧客統合ID基盤（共通システム）](common-customer-identity-system.md)

本事業部の `member_id` は `ドメイン別IDマッピング`（domain=`ecommerce`）で `unified_customer_id` と紐付く。

## 機能補完データモデル
_対象業務システムの各機能を満たすために必要なモデル_

### 会員マスタ（会員・ポイント管理システム）
| 項目 | 型 | 説明 |
|---|---|---|
| member_id | string | 会員ID |
| member_name | string | 会員名 |
| email | string | メールアドレス |
| rank | string | 会員ランク |
| registered_at | date | 登録日 |
| region | string | 居住地域 |
| age_band | string | 年齢帯 |

### 出店者・サプライヤーマスタ（商品管理システム）
| 項目 | 型 | 説明 |
|---|---|---|
| seller_id | string | 出店者ID |
| seller_name | string | 出店者名 |
| seller_country | string | 所在国 |
| settlement_currency | string | 決済通貨 |
| cross_border_flag | boolean | 越境EC対象有無 |
| contract_start_date | date | 出店契約開始日 |

### カテゴリマスタ（商品管理システム）
| 項目 | 型 | 説明 |
|---|---|---|
| category_id | string | カテゴリID |
| category_name | string | カテゴリ名 |
| parent_category_id | string | 親カテゴリID（NULL=最上位） |
| import_ratio | decimal | 輸入品比率 |
| demand_elasticity | decimal | 需要弾力性 |

### ポイント残高・付与・利用履歴（会員・ポイント管理システム）
| 項目 | 型 | 説明 |
|---|---|---|
| point_event_id | string | ポイントイベントID |
| member_id | string | 会員ID |
| event_type | string | 付与/利用/失効/調整 |
| point_amount | integer | ポイント数 |
| balance_after | integer | イベント後残高 |
| related_order_id | string | 関連受注ID |
| event_at | datetime | 発生日時 |
| expiry_at | datetime | 有効期限 |

### 返品・キャンセルデータ（受注管理システム）
| 項目 | 型 | 説明 |
|---|---|---|
| return_id | string | 返品ID |
| order_id | string | 元受注ID |
| member_id | string | 会員ID |
| sku | string | SKU |
| return_reason | string | 返品理由 |
| return_amount | decimal | 返品金額 |
| returned_at | datetime | 返品日時 |

## 業務システム別データモデルマッピング

| 業務システム | 使用するデータモデル |
|---|---|
| 商品管理システム | 商品マスタ、カテゴリマスタ、出店者・サプライヤーマスタ、価格ルール |
| 受注管理システム | 受注データ、返品・キャンセルデータ |
| 在庫管理システム | 在庫データ、配送・物流ルート |
| 会員・ポイント管理システム | 会員マスタ、ポイント残高・付与・利用履歴、IDマッピング |
| マーケティング/キャンペーン管理システム | ポイント・キャンペーンデータ、会員行動データ、キャンペーン反応履歴 |

## ニュースから得たい内部インサイト
- 為替急変: 越境ECや海外仕入の粗利と価格転嫁余地
- 競合統合: ポイント還元率と離反率の関係
- 金融政策転換: 消費マインド低下がカテゴリ需要に与える影響
