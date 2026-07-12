# DS: fintech_ai Lakehouse 定義

## 前提
- 本文書は nexus6-se-workshop の Demo 用 DS.md である。
- 数値例、テーブル名、業務事例は架空であり、実在企業の機密を含まない。
- Agent 2・3 が Fabric クエリを組み立てる前に参照する。
- SQL の方言は Fabric SQL Analytics Endpoint を想定する。
- 対象 Lakehouse: `fintech_ai`。

## 対象範囲
- Fintech の口座、カード決済、FX、与信、ローン、収益リスク分析。
- Bronze/Silver/Gold の違いがある場合、業務判断は Gold を優先する。
- 明細確認が必要な場合のみ Silver を参照する。
- 速報分析では最新スナップショットと直近月次集計を併用する。

## 対象テーブル
| テーブル | 業務意味 | 主なキー |
|---|---|---|
| `fintech.accounts` | 顧客口座。口座種別、残高、開設日を保持 | `account_id` |
| `fintech.card_transactions` | カード・決済明細。海外フラグと金額を保持 | `transaction_id` |
| `fintech.fx_positions` | FX・証券・暗号資産ポジション | `position_id` |
| `fintech.fx_rate_snapshots` | 通貨ペア別レートスナップショット | `rate_snapshot_id` |
| `fintech.loan_balances` | 住宅ローン、カードローン、リボ残高 | `loan_id` |
| `fintech.revenue_risk` | 手数料、金利、為替収益、リスク損失 | `revenue_id` |

## 主要 KPI 定義
| KPI | 計算式 | 粒度 | 注意 |
|---|---|---|---|
| FXエクスポージャー | `SUM(position_amount * mid_rate)` | 日×通貨 | ポジション方向を分ける |
| 海外決済比率 | `SUM(overseas_amount)/SUM(amount)` | 日×MCC | 返品控除後 |
| NIM | `(interest_revenue-interest_expense)/earning_assets` | 月 | Demo では概算 |
| 延滞率 | `COUNT(overdue_flag=true)/COUNT(*)` | 月×ローン種別 | 満期超過日数は別計算 |

## 用語・コード値
| 用語 | データ表現 | 補足 |
|---|---|---|
| KYC完了 | `kyc_status = '完了'` | 本人確認済み |
| 海外決済 | `overseas_flag = true` | 海外加盟店または外貨決済 |
| リボ払い | `loan_type = 'リボ払い'` | カードリボ残高 |

## データ品質・除外条件
- fx_positions は日次スナップショット。最新日だけでなく前日差を確認する。
- pnl_amount は商品通貨建ての場合があるため、JPY 換算列がある場合はそちらを優先する。
- overdue_flag は延滞中を示し、過去延滞履歴ではない。
- 個人を特定する粒度での出力は禁止する。出力はセグメントまたは集計単位にする。
- 金額は JPY 換算値を優先し、外貨建て値は補助情報として扱う。
- ニュース日当日のみではなく、前後期間を比較して一時ノイズを避ける。

## 推奨クエリパターン
### 通貨別 FX 損益
```sql
SELECT market_currency, product_type, SUM(pnl_amount) AS pnl_amount
FROM fintech.fx_positions
GROUP BY market_currency, product_type
ORDER BY pnl_amount ASC;
```

### ローン種別延滞率
```sql
SELECT loan_type,
       SUM(CASE WHEN overdue_flag THEN 1 ELSE 0 END) * 1.0 / COUNT(*) AS overdue_rate
FROM fintech.loan_balances
GROUP BY loan_type;
```

## 3 シナリオでの使い分け
- Fintech では為替は市場リスク、競合はカードシェア、利上げは利ざやと信用コストとして分ける。
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
| account_id | 口座ID | - | 口座単位の内部キー。表示では口座数等に集計する。 |
| user_id | ユーザーID | - | 顧客単位の内部キー。個人特定を避けて扱う。 |
| account_type | 口座種別 | - | 普通、投資、決済などの口座区分。 |
| balance | 口座残高 | 円 | 口座残高。 |
| transaction_id | 決済ID | - | カード決済明細の識別子。 |
| transaction_date | 決済日 | 日付 | 決済発生日。 |
| amount | 決済金額 | 円/外貨 | カード決済額。 |
| overseas_flag | 海外決済フラグ | 真偽値 | 海外加盟店または外貨決済の判定。 |
| position_amount | ポジション金額 | 円/外貨 | FX・証券等の保有額。 |
| pnl_amount | 評価損益 | 円/外貨 | ポジションの損益。JPY 換算を優先する。 |
| market_currency | 市場通貨 | - | ポジションが参照する通貨。 |
| fee_revenue | 手数料収益 | 円 | 決済・取引等から得る手数料収益。 |
| interest_revenue | 金利収益 | 円 | 貸出等から得る金利収益。 |
| fx_revenue | 為替収益 | 円 | 為替取引・換算から得る収益。 |
| risk_loss | リスク損失 | 円 | 与信・市場リスクに伴う損失。 |
| loan_id | ローンID | - | ローン契約の識別子。 |
| loan_type | ローン種別 | - | 住宅ローン、カードローン、リボ払い等。 |
| principal_balance | ローン元本残高 | 円 | ローンの元本残高。 |
| loan_balance | ローン残高 | 円 | ローン元本残高または集計済み残高。 |
| interest_rate | 貸出金利 | % | ローン契約の適用金利。 |
| avg_interest_rate | 平均貸出金利 | % | 対象ローン群の加重平均金利。 |
| monthly_payment | 月額返済額 | 円 | ローンの毎月返済額。 |
| overdue_flag | 延滞フラグ | 真偽値 | 現在延滞中かどうか。 |
| fx_position_usd | USD建てFXポジション | USD | USD 建て市場リスク量。 |
| loan_delinquency_rate | ローン延滞率 | % | ローン件数に対する延滞割合。 |
| year_month | 対象年月 | yyyy-MM | Gold リスクサマリの集計対象月。 |
| metric_name | KPI物理名 | - | `fintech_ai.risk_summary` の KPI 名。表示では本表の論理名に変換する。 |
| metric_value | KPI値 | 指標依存 | `metric_name` に対応する数値。`metric_unit` と本表の単位に従って表示する。 |
| metric_unit | KPI単位 | 指標依存 | KPI 値の単位。JPY は円、件数は件、比率は % に正規化する。 |
| description | KPI説明 | - | KPI の業務説明。 |
| fx_position_pnl | FXポジション損益合計 | 円 | FX ポジションの JPY 換算評価損益合計。 |
| overseas_card_amount | 海外カード決済額 | 円 | 海外フラグ付きカード決済額の JPY 換算合計。 |
| negative_credit_reviews | 否認・保留審査件数 | 件 | 与信審査で否認または保留となった件数。 |
