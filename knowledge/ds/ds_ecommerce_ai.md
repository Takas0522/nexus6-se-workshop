# DS: ecommerce_ai Lakehouse 定義

## 前提
- 本文書は nexus6-se-workshop の Demo 用 DS.md である。
- 数値例、テーブル名、業務事例は架空であり、実在企業の機密を含まない。
- Agent 2・3 が Fabric クエリを組み立てる前に参照する。
- SQL の方言は Fabric SQL Analytics Endpoint を想定する。
- 対象 Lakehouse: `ecommerce_ai`。

## 対象範囲
- Eコマースの受注、商品、在庫、会員、ポイント、キャンペーン分析。
- Bronze/Silver/Gold の違いがある場合、業務判断は Gold を優先する。
- 明細確認が必要な場合のみ Silver を参照する。
- 速報分析では最新スナップショットと直近月次集計を併用する。

## 対象テーブル
| テーブル | 業務意味 | 主なキー |
|---|---|---|
| `ecommerce.orders` | 受注明細。売上、数量、SKU、会員を保持 | `order_id` |
| `ecommerce.products` | 商品マスタ。原価、販売価格、仕入通貨を保持 | `sku` |
| `ecommerce.inventory` | 在庫。倉庫、数量、輸入通貨、入荷日を保持 | `inventory_id` |
| `ecommerce.point_events` | ポイント付与、利用、失効、残高を保持 | `point_event_id` |
| `ecommerce.member_behaviors` | 閲覧、カート、購入、離脱イベント | `event_id` |
| `ecommerce.categories` | カテゴリ需要弾力性と輸入品比率 | `category_id` |

## 主要 KPI 定義
| KPI | 計算式 | 粒度 | 注意 |
|---|---|---|---|
| CVR | `purchase_events/view_events` | 日×カテゴリ | bot/社内アクセスは除外 |
| カート離脱率 | `abandon_events/cart_events` | 日×会員ランク | セール日を別管理 |
| 粗利率 | `SUM(selling_price-cost_price)/SUM(selling_price)` | 月×SKU | 外貨原価は JPY 換算 |
| ポイント原資率 | `SUM(points_granted_jpy)/SUM(order_amount)` | 月×キャンペーン | 失効戻入は除外 |

## 用語・コード値
| 用語 | データ表現 | 補足 |
|---|---|---|
| Gold会員 | `rank = 'Gold'` | 優先監視セグメント |
| 越境SKU | `cross_border_flag = true OR procurement_currency <> 'JPY'` | 為替影響対象 |
| 離脱 | `event_type = '離脱'` | カート離脱とは区別 |

## データ品質・除外条件
- 返品は売上控除対象。返品率を見る場合は returns を別集計する。
- arrival_date が古い在庫は円安前仕入の可能性があるため、仕入時点の通貨条件を確認する。
- point_events の balance_after はイベント後残高であり、月末残高ではない。
- 個人を特定する粒度での出力は禁止する。出力はセグメントまたは集計単位にする。
- 金額は JPY 換算値を優先し、外貨建て値は補助情報として扱う。
- ニュース日当日のみではなく、前後期間を比較して一時ノイズを避ける。

## 推奨クエリパターン
### 会員ランク別カート離脱
```sql
SELECT m.rank, COUNT(*) AS abandon_events
FROM ecommerce.member_behaviors b
JOIN ecommerce.members m ON b.member_id = m.member_id
WHERE b.event_type = '離脱'
GROUP BY m.rank;
```

### 越境 SKU 粗利
```sql
SELECT p.category, SUM(o.order_amount - p.cost_price * o.quantity) AS gross_profit
FROM ecommerce.orders o
JOIN ecommerce.products p ON o.sku = p.sku
WHERE p.procurement_currency <> 'JPY'
GROUP BY p.category;
```

## 3 シナリオでの使い分け
- Eコマースでは為替は粗利、競合はポイント、利上げは需要下押しとして切り分ける。
- シナリオ1は為替・外貨・海外フラグを優先する。
- シナリオ2は競合、ポイント、乗換、決済シェアの変化を優先する。
- シナリオ3は金利、返済、需要、資金コストの変化を優先する。

## Agent への補助指示
- SQL 生成前に対象 KPI、期間、粒度、除外条件を短く宣言する。
- 集計結果が 0 件の場合は「影響なし」ではなく「データなし」と返す。
- しきい値判定は Skill.md の基準を参照し、DS.md だけで結論を出さない。
- 不明なカラムを推測で作らず、利用可能なテーブル一覧に戻る。

## 更新フロー
- テーブル追加時は本 DS.md の対象テーブルと KPI 定義を更新する。
- Skill.md と関連する KPI 名は表記をそろえる。
- 更新後は ADLS の `skill-docs/ds-docs/` にアップロードする。

## 物理項目 ↔ 論理項目（日本語ラベル）対応

| 物理列名 | 論理名 (ja) | 単位 | 説明 |
|---|---|---|---|
| order_id | 注文ID | - | 受注明細の識別子。表示時は注文件数等に集計する。 |
| member_id | 会員ID | - | 会員単位の内部キー。個人を特定しない粒度で扱う。 |
| sku | SKU | - | 商品単位の識別子。 |
| order_date | 注文日 | 日付 | 売上・需要変化を見る日付。 |
| quantity | 注文数量 | 個 | 商品の販売数量。 |
| order_amount | 注文金額 | 円 | 返品控除前後の売上金額。 |
| product_name | 商品名 | - | 商品表示名。 |
| category | 商品カテゴリ | - | 需要・粗利を見る分類。 |
| procurement_currency | 仕入通貨 | - | 外貨仕入の判定に使う通貨。 |
| cost_price | 仕入原価 | 円/外貨 | 商品原価。JPY 換算値があれば優先する。 |
| selling_price | 販売価格 | 円 | 販売単価。 |
| inventory_id | 在庫ID | - | 在庫明細の識別子。 |
| stock_qty | 在庫数量 | 個 | 倉庫別の在庫数。 |
| arrival_date | 入荷日 | 日付 | 仕入時点の為替条件を確認する日付。 |
| import_currency | 輸入通貨 | - | 輸入在庫の元通貨。 |
| point_rate | ポイント還元率 | % | キャンペーン時の付与率。 |
| campaign_budget | キャンペーン予算 | 円 | 販促・ポイント原資の予算。 |
| event_type | 会員行動種別 | - | 閲覧、カート、購入、離脱などの行動。 |
| cart_abandon_count | カート離脱件数 | 件 | カート投入後に購入へ進まなかった件数。 |
| cross_border_cost | 越境 EC 仕入コスト | 円 | 越境 SKU の仕入・為替影響コスト。 |
| crossborder_fx_cost_jpy | 越境EC為替コスト | 円 | 外貨仕入の JPY 換算コスト増減。 |
| campaign_roi | キャンペーンROI | 倍 | 販促費に対する売上・粗利効果。 |
| cross_border_flag | 越境取引フラグ | 真偽値 | 海外セラー・越境 SKU の判定。 |
| year_month | 対象年月 | yyyy-MM | Gold リスクサマリの集計対象月。 |
| metric_name | KPI物理名 | - | `ecommerce_ai.risk_summary` の KPI 名。表示では本表の論理名に変換する。 |
| metric_value | KPI値 | 指標依存 | `metric_name` に対応する数値。`metric_unit` と本表の単位に従って表示する。 |
| metric_unit | KPI単位 | 指標依存 | KPI 値の単位。JPY は円、件数は件、比率は % に正規化する。 |
| description | KPI説明 | - | KPI の業務説明。 |
| gross_margin_rate | 粗利率 | % | 売上に対する粗利の割合。 |
| point_cost | ポイント還元コスト | point | 付与・還元されたポイント原資の合計。 |
| campaign_reactions | キャンペーン反応件数 | 件 | キャンペーンに対する反応イベント件数。 |
