# 29. Azure Functions Cron Trigger（JST 12:00）導入

関連: [開発計画 README](README.md) | [Foundry Trigger 引継ぎ](16-foundry-trigger-wiring.md) | [未解決 TODO](19-open-items.md)

---

## 目的

Foundry Scheduled Trigger の API スキーマが不安定で自動作成できないため、代替として Azure Functions Timer Trigger を導入し、`news-analysis-jobs` キューへの定期 enqueue を実現する。

---

## 作成した実装

新規プロジェクト:

- `src/news-trigger-function/NewsPortalTriggerFunction.csproj`
- `src/news-trigger-function/Program.cs`
- `src/news-trigger-function/NewsPortalPollerFunction.cs`
- `src/news-trigger-function/host.json`
- `src/news-trigger-function/local.settings.json.example`

機能:

1. Timer Trigger で定期実行
2. News Portal (`stnexus6portal1t2i`) を巡回し `article-*` リンクを収集
3. 記事本文を抽出して `NewsAnalysisJob` JSON を作成
4. Azure Storage Queue `news-analysis-jobs` に enqueue

---

## スケジュール設定

- 設定キー: `DailyRunCron`
- 設定値: `0 0 3 * * *`
- 意味: **毎日 UTC 03:00 = 日本時間 12:00**

> Azure Functions の Timer Trigger は UTC 基準で運用し、JST 変換は cron 値で吸収する。

---

## Azure リソース

新規:

- Function App: `func-nexus6-swc-062801`
- ホスト用 Storage: `stn6func062801`
- App Service Plan: `plan-nexus6-func` (Linux B1)

既存連携:

- Queue 送信先 Storage: `stnexus6skill1t2i`
- Queue 名: `news-analysis-jobs`
- Application Insights: `appi-nexus6-swc`

---

## 権限

Function App の System Assigned Managed Identity に以下を付与:

- `Storage Queue Data Message Sender`
  - Scope: `stnexus6skill1t2i`

---

## デプロイ結果

- `dotnet publish` + `az functionapp deployment source config-zip` で反映
- Function 一覧に `NewsPortalPoller` が登録されることを確認
- Function App の主要設定を確認:
  - `FUNCTIONS_WORKER_RUNTIME=dotnet-isolated`
  - `FUNCTIONS_EXTENSION_VERSION=~4`
  - `DailyRunCron=0 0 3 * * *`
  - `Storage__Account=stnexus6skill1t2i`
  - `Storage__QueueName=news-analysis-jobs`

---

## 補足

- Foundry 側の `/schedules` は引き続き `value: []`（未作成）
- 定期起動は Functions 側で代替済みのため、運用上の定期実行要件は満たす
