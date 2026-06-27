# Adaptive Card 仕上げ

## 対象

- トラック: `y-card-polish`
- 対象: Agent 2/3 の KPI 出力、Agent 4 の Adaptive Card、DS.md マッピング表

## 数値フォーマッタ

`KpiValueFormatter` を追加し、LLM 出力に依存せずサーバーサイドで値を整形する。

| 単位 | 表示 |
|---|---|
| `JPY` / 円 | `26,325,000 円` |
| 件 / 人 / 個 | `2 件` |
| `%` | `avg_interest_rate=0.0500` は `5.00 %`、粗利率などの比率は `35.9 %` |
| `ratio` | `1.42 倍` |

本文・Next Actions では単位付き数値をポストプロセスし、KPI 表の値列は単位込みの整形済み文字列にする。

## Next Actions

Agent 3 の `next_actions` は以下の構造を標準にする。

```json
{ "title": "30字以内の要点", "body": "具体施策と数値根拠" }
```

Adaptive Card では各 action を Container とし、見出し TextBlock（Bolder / Accent）と本文 TextBlock の 2 段で表示する。旧形式の文字列配列が返った場合は、改行または最初の句点で title/body に分割する。

## Web リファレンス

- `example.com/mock-*` は WebResearch と通知直前の両方で除外する。
- Mock fallback は URL を生成しない。
- 有効な URL がない場合、Web リファレンスセクションは非表示にする。

## DS.md 補完

以下の Gold KPI 名を DS.md のマッピング表へ追加した。

- fintech: `fx_position_pnl`, `overseas_card_amount`, `negative_credit_reviews`
- mobile: `device_subsidy`, `mnp_out_count`, `installment_payment`, `cancel_ticket_count`
- ecommerce: `gross_margin_rate`, `point_cost`, `campaign_reactions`
- monthly revenue 共通: cost / FX sensitivity / KPI summary 系の補助列

## Before / After

- Before: `26325000.0000`、`0.0500`、長文 NextAction 1 段落、mock URL 表示。
- After: `26,325,000 円`、`5.00 %`、title/body 2 段、mock URL 非表示。

## 検証結果

| 項目 | 結果 |
|---|---|
| `dotnet test src/news-analysis-agent/NewsAnalysisAgent.sln --no-restore` | Unit 24 / Integration 1 passed |
| Docker build | `nexus6-news-analysis-agent:card-polish` |
| ACR image | `crnexus6swc.azurecr.io/nexus6-hosted-agent:y-card-polish-20260627-1` |
| ACA revision | `ca-nexus6-hosted-agent--0000018` Running / Healthy |
| ADLS | `stnexus6skill1t2i/skill-docs/ds-docs/` に DS.md 4 本を上書き |
| Vector Store | `vs_EN0WyOWKa7aVn0STee8oFhZ7` に DS.md 4 本を再 upload / attach、13 files completed |
| E2E | S1/S2/S3 `/devui/run` で payloadJson を検査し、KPI 値整形、NextActions 2 段構造、mock URL 排除、物理列名非表示を確認 |

## 未対応項目

- 現時点の追加未対応はなし。
