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
