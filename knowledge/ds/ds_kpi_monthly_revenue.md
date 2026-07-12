# DS: 月次収益 KPI 共通定義

## 前提
- 本文書は nexus6-se-workshop の Demo 用 DS.md である。
- 数値例、テーブル名、業務事例は架空であり、実在企業の機密を含まない。
- Agent 2・3 が Fabric クエリを組み立てる前に参照する。
- SQL の方言は Fabric SQL Analytics Endpoint を想定する。
- 対象 Lakehouse: `lh_nexus6_gold`。

## 対象範囲
- 3 事業部の売上、粗利、費用、リスク損失を横断比較する共通 KPI。
- Bronze/Silver/Gold の違いがある場合、業務判断は Gold を優先する。
- 明細確認が必要な場合のみ Silver を参照する。
- 速報分析では最新スナップショットと直近月次集計を併用する。

## 対象テーブル
| テーブル | 業務意味 | 主なキー |
|---|---|---|
| `gold.monthly_kpi_summary` | 事業部・月・KPI 単位の集計済み指標 | `year_month, business_domain, kpi_name` |
| `gold.monthly_revenue` | 売上、原価、粗利、費用の月次集計 | `year_month, business_domain` |
| `gold.news_impact_results` | Agent 2・3 の影響判定ログ | `impact_id` |

## 主要 KPI 定義
| KPI | 計算式 | 粒度 | 注意 |
|---|---|---|---|
| 月次売上 | `SUM(revenue_jpy)` | 事業部×月 | 返品控除後 |
| 粗利率 | `SUM(gross_profit_jpy)/SUM(revenue_jpy)` | 事業部×月 | 売上 0 は除外 |
| リスク損失率 | `SUM(risk_loss_jpy)/SUM(revenue_jpy)` | 事業部×月 | Fintech 以外は 0 の場合あり |
| 施策費率 | `SUM(campaign_cost_jpy)/SUM(revenue_jpy)` | 事業部×月 | ポイント原資を含む |

## 用語・コード値
| 用語 | データ表現 | 補足 |
|---|---|---|
| 事業部 | `business_domain IN mobile/ecommerce/fintech` | ドメイン名は英小文字 |
| 月次 | `year_month` | yyyy-MM |
| 影響判定 | `impact_level` | none/watch/action/urgent |

## データ品質・除外条件
- 月次テーブルは締め後に確定するため、当月速報は暫定値として扱う。
- 事業部間で売上認識が異なるため、横断比較は変化率を優先する。
- news_impact_results は Agent 出力であり、財務確定値ではない。
- 個人を特定する粒度での出力は禁止する。出力はセグメントまたは集計単位にする。
- 金額は JPY 換算値を優先し、外貨建て値は補助情報として扱う。
- ニュース日当日のみではなく、前後期間を比較して一時ノイズを避ける。

## 推奨クエリパターン
### 月次 KPI の基本形
```sql
SELECT
    year_month,
    business_domain,
    kpi_name,
    kpi_value
FROM gold.monthly_kpi_summary
WHERE year_month >= '2025-01'
ORDER BY year_month, business_domain, kpi_name;
```

## 3 シナリオでの使い分け
- 共通 KPI は 3 シナリオすべてで最終影響額をそろえるために使う。
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
| year_month | 対象年月 | yyyy-MM | 月次集計の対象月。 |
| business_domain | 事業ドメイン | - | mobile / ecommerce / fintech の横断区分。 |
| division | 事業部 | - | 月次収益テーブルで使う事業部区分。 |
| kpi_name | KPI名 | - | 集計済み指標の名称。表示では日本語名を使う。 |
| kpi_value | KPI値 | 指標依存 | KPI の数値。単位列または DS 定義に従う。 |
| revenue_jpy | 売上 | 円 | 返品控除後の売上。 |
| gross_revenue_jpy | 月次売上 | 円 | 事業部別の総売上。 |
| total_cost_jpy | 総コスト | 円 | 売上に対応する原価・費用の合計。 |
| gross_margin_jpy | 粗利 | 円 | 売上から原価を控除した利益。 |
| gross_margin_rate | 粗利率 | % | 売上に対する粗利の割合。 |
| risk_loss_jpy | リスク損失 | 円 | 信用・市場・運用リスクによる損失額。 |
| campaign_cost_jpy | 施策費 | 円 | ポイント原資や販促費を含む施策費。 |
| fx_exposure_usd | USD為替エクスポージャー | USD | USD 建ての為替リスク量。 |
| fx_exposure_other_jpy | その他通貨為替エクスポージャー | 円 | USD 以外の通貨リスクを JPY 換算した額。 |
| active_customer_count | アクティブ顧客数 | 人 | 対象月に有効な顧客数。 |
| churned_customer_count | 離脱顧客数 | 人 | 対象月に離脱した顧客数。 |
| impact_id | 影響判定ID | - | Agent 2・3 の判定ログ識別子。 |
| impact_level | 影響レベル | - | none / watch / action / urgent の判定。 |
| cost_type | コスト種別 | - | 月次コスト詳細の分類。device_cost / cost_of_goods / risk_loss など。 |
| currency | 通貨 | - | 為替感応度テーブルの対象通貨。 |
| avg_rate | 平均為替レート | 円/外貨 | 対象年月の平均為替レート。 |
| exposure_amount | エクスポージャー額 | USD | 為替感応度算出に使う外貨エクスポージャー。 |
| metric_name | KPI物理名 | - | 月次 KPI サマリの KPI 名。表示では日本語論理名を使う。 |
| metric_unit | KPI単位 | 指標依存 | KPI 値の単位。JPY は円、件数は件、比率は % に正規化する。 |
| description | KPI説明 | - | KPI の業務説明。 |
