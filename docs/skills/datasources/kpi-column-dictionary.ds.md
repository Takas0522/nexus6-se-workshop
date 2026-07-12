# DS: KPI カラム辞書 — 物理名→論理名マッピング

## 前提

- 本文書は Hosted Agent が Fabric KPI テーブルの出力を人間にわかりやすく表示するための辞書である。
- Agent がインパクト分析結果を出力する際は、**物理カラム名ではなく論理名（日本語）で表記**すること。
- 金額は百万円単位（例: 302,000,000 → 3.02億円）で、率は %、人数は「人」で出力する。

---

## kpi_monthly_revenue テーブル

Gold 層の月次 KPI 集計テーブル。各事業部の売上・コスト・粗利・顧客メトリクスを格納する。

| 物理カラム名 | 論理名（日本語） | 単位 | 説明 |
|---|---|---|---|
| `year_month` | 対象年月 | yyyy-MM | 集計対象の年月 |
| `division` | 事業部 | — | 事業部識別子 |
| `gross_revenue_jpy` | 月次売上高 | 円 | 当月の売上総額 |
| `total_cost_jpy` | 総コスト | 円 | 当月の総費用 |
| `gross_margin_jpy` | 粗利額 | 円 | 売上高 − 総コスト |
| `gross_margin_rate` | 粗利率 | % | 粗利額 ÷ 売上高 × 100 |
| `active_customer_count` | アクティブ顧客数 | 人 | 当月にアクティブな顧客数 |
| `churn_rate` | 解約率 | % | 当月の顧客離脱率 |
| `churned_customer_count` | 離脱顧客数 | 人 | 当月に離脱した顧客数 |
| `fx_exposure_usd` | USD為替エクスポージャー | USD | USD建て為替リスク額 |
| `fx_exposure_other_jpy` | その他通貨エクスポージャー | 円 | USD以外の為替リスク円換算額 |

---

## {division}_ai_risk_summary テーブル

Gold 層の事業部別 AI リスクサマリ。メダリオン ETL が Bronze → Silver → Gold と集約した結果。

| 物理カラム名 | 論理名（日本語） | 単位 | 説明 |
|---|---|---|---|
| `year_month` | 対象年月 | yyyy-MM | 集計対象の年月 |
| `metric_name` | 指標名 | — | KPI 指標の物理名（下表参照） |
| `metric_value` | 指標値 | 各指標による | KPI の数値 |
| `metric_unit` | 単位 | — | 指標の単位表記 |
| `description` | 説明 | — | 指標の日本語説明 |

### risk_summary の metric_name → 論理名

| metric_name（物理名） | 論理名（日本語） | 単位 |
|---|---|---|
| `mnp_out_rate` | MNP転出率 | % |
| `device_fx_cost_jpy` | 海外端末仕入コスト | 円 |
| `campaign_roi` | キャンペーンROI | 倍 |
| `crossborder_fx_cost_jpy` | 越境EC為替コスト | 円 |
| `fx_position_usd` | USD建てFXポジション | USD |
| `loan_delinquency_rate` | ローン延滞率 | % |
| `ad_revenue_jpy` | 広告収益 | 円 |
| `dau` | DAU（日次アクティブユーザー） | 人 |
| `subscription_revenue_jpy` | サブスクリプション収益 | 円 |
| `content_production_cost_jpy` | コンテンツ制作費 | 円 |
| `streaming_infra_cost_jpy` | 配信インフラコスト | 円 |
| `game_revenue_jpy` | ゲーム売上 | 円 |
| `in_app_purchase_jpy` | アプリ内課金額 | 円 |
| `server_cost_jpy` | サーバーコスト | 円 |
| `concurrent_users` | 同時接続ユーザー数 | 人 |

---

## 出力フォーマット指示

Agent がインパクト分析結果や KPI 値を出力する際は以下のルールに従うこと:

1. **物理名ではなく論理名で表記**: `gross_revenue_jpy` → 「月次売上高」
2. **金額のフォーマット**: 
   - 1億円以上: `X.XX億円`（例: 302,000,000 → 3.02億円）
   - 1000万円以上: `X,XXX万円`（例: 45,000,000 → 4,500万円）
   - それ未満: `X,XXX円`
3. **率のフォーマット**: `XX.X%`（小数第1位まで）
4. **人数のフォーマット**: カンマ区切り + 「人」（例: 30,000人）
5. **テーブル参照時**: `kpi_monthly_revenue` → 「月次KPIサマリ」、`{div}_ai_risk_summary` → 「{事業部名}リスクサマリ」
