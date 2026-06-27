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
