# DS: mobile_ai Lakehouse 定義

## 前提
- 本文書は nexus6-se-workshop の Demo 用 DS.md である。
- 数値例、テーブル名、業務事例は架空であり、実在企業の機密を含まない。
- Agent 2・3 が Fabric クエリを組み立てる前に参照する。
- SQL の方言は Fabric SQL Analytics Endpoint を想定する。
- 対象 Lakehouse: `mobile_ai`。

## 対象範囲
- モバイル通信の契約、請求、端末、MNP、分割払い分析。
- Bronze/Silver/Gold の違いがある場合、業務判断は Gold を優先する。
- 明細確認が必要な場合のみ Silver を参照する。
- 速報分析では最新スナップショットと直近月次集計を併用する。

## 対象テーブル
| テーブル | 業務意味 | 主なキー |
|---|---|---|
| `mobile.contracts` | 契約マスタ。更新月、プラン、端末種別を保持 | `contract_id` |
| `mobile.usage_billing` | 月次利用・請求。ARPU と請求変化を算出 | `usage_id` |
| `mobile.device_costs` | 端末・設備コスト。通貨と調達地域を保持 | `cost_item_id` |
| `mobile.mnp_history` | MNP 転出入履歴。競合キャンペーン起因を保持 | `mnp_id` |
| `mobile.installment_details` | 端末分割払い。金利、残回数、月額を保持 | `installment_id` |

## 主要 KPI 定義
| KPI | 計算式 | 粒度 | 注意 |
|---|---|---|---|
| ARPU | `SUM(monthly_charge)/COUNT(DISTINCT contract_id)` | 月 | 法人/個人は分ける |
| MNP転出率 | `COUNT(mnp_type='転出')/active_contracts` | 月 | 更新月対象を別集計 |
| 端末粗利圧縮率 | `fx_cost_increase_jpy/gross_profit_jpy` | 月×SKU | 国内調達は除外 |
| 変動金利分割比率 | `SUM(variable_installment_balance)/SUM(total_amount)` | 月 | 金利型が不明な行は除外 |

## 用語・コード値
| 用語 | データ表現 | 補足 |
|---|---|---|
| SIMのみ | `device_type = 'SIM_ONLY'` | 端末購入なし |
| 更新月 | `update_month` | 乗換リスクが高い月 |
| 競合キャンペーン | `trigger_reason = '競合キャンペーン'` | MNP 理由 |

## データ品質・除外条件
- resolved_at が NULL の問い合わせは未解決であり、解約済みとは扱わない。
- subsidy_amount は月次補助であり、端末一括割引とは異なる。
- installment_details の interest_rate が NULL の場合は旧契約の固定扱いではなく不明として除外する。
- 個人を特定する粒度での出力は禁止する。出力はセグメントまたは集計単位にする。
- 金額は JPY 換算値を優先し、外貨建て値は補助情報として扱う。
- ニュース日当日のみではなく、前後期間を比較して一時ノイズを避ける。

## 推奨クエリパターン
### MNP と更新月
```sql
SELECT update_month, COUNT(*) AS contracts
FROM mobile.contracts
WHERE update_month BETWEEN '2025-06' AND '2025-08'
GROUP BY update_month
ORDER BY update_month;
```

### 外貨建て端末コスト
```sql
SELECT currency, vendor_region, SUM(unit_cost) AS cost_original
FROM mobile.device_costs
WHERE currency <> 'JPY'
GROUP BY currency, vendor_region;
```

## 3 シナリオでの使い分け
- モバイルでは為替、競合 MNP、利上げ分割払いの 3 方向を同じ契約母集団で比較する。
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
| contract_id | 契約ID | - | 契約単位の集計キー。カード表示では件数集計名に言い換える。 |
| customer_id | 顧客ID | - | 顧客単位の内部キー。個人特定を避け、セグメント集計で扱う。 |
| plan_id | 料金プランID | - | 契約中プランの識別子。 |
| device_type | 端末種別 | - | SIMのみ、スマートフォン等の区分。 |
| update_month | 更新月 | yyyy-MM | MNP・解約リスクが高まる契約更新月。 |
| subsidy_amount | 端末補助額 | 円 | 端末割引・補助の原資。 |
| usage_date | 利用日 | 日付 | 利用・請求実績の日付。 |
| voice_usage | 音声利用量 | 分 | 音声通話の利用量。 |
| data_usage | データ利用量 | GB | データ通信の利用量。 |
| monthly_charge | 月額請求額 | 円 | 月次 ARPU 算出に使う請求額。 |
| currency | 取引通貨 | - | 端末・設備コストの元通貨。 |
| unit_cost | 単価 | 円/外貨 | 調達単価。JPY 換算値があればそちらを優先する。 |
| procurement_date | 調達日 | 日付 | 端末・設備の仕入タイミング。 |
| vendor_region | 仕入先地域 | - | 為替・地政学リスクの対象地域。 |
| mnp_type | MNP種別 | - | 転出・転入の区分。 |
| trigger_reason | MNP理由 | - | 競合キャンペーン等の転出入理由。 |
| total_amount | 分割払い総額 | 円 | 端末分割払いの元本総額。 |
| monthly_payment | 月額分割支払額 | 円 | 契約者の月次支払負担。 |
| remaining_months | 残支払回数 | 月 | 分割払いの残期間。 |
| interest_rate | 分割払い金利 | % | 変動金利影響を見るための金利。 |
| mnp_out_rate | MNP転出率 | % | 契約母集団に対する MNP 転出割合。 |
| device_fx_cost_jpy | 海外端末仕入コスト | 円 | 為替影響を受ける端末仕入コスト。 |
| overseas_procurement_cost | 海外調達コスト | 円 | 海外由来の端末・設備調達コスト。 |
| year_month | 対象年月 | yyyy-MM | Gold リスクサマリの集計対象月。 |
| metric_name | KPI物理名 | - | `mobile_ai.risk_summary` の KPI 名。表示では本表の論理名に変換する。 |
| metric_value | KPI値 | 指標依存 | `metric_name` に対応する数値。`metric_unit` と本表の単位に従って表示する。 |
| metric_unit | KPI単位 | 指標依存 | KPI 値の単位。JPY は円、件数は件、比率は % に正規化する。 |
| description | KPI説明 | - | KPI の業務説明。 |
| device_subsidy | 端末補助額合計 | 円 | 端末購入補助・割引原資の合計。 |
| mnp_out_count | MNP転出件数 | 件 | MNP 種別が転出の件数。 |
| installment_payment | 分割払い月額合計 | 円 | 端末分割払いの月額支払合計。 |
| cancel_ticket_count | 解約問い合わせ件数 | 件 | 解約意向を含む問い合わせ件数。 |
