# 18. Observability Check

関連: [開発計画 README](README.md) | [Scenario Verification](17-scenario-verification.md)

---

## 概要

Phase 4 トラック O `o-observability` として、App Insights `appi-nexus6-swc` に Phase 3 E2E 実行（S1/S2/S3）由来のテレメトリが流入していることを確認した。

| 項目 | 値 |
|---|---|
| 実行日時 | 2026-06-27T10:09Z |
| Resource Group | `rg-nexus6-swc` |
| App Insights | `appi-nexus6-swc` |
| ACA | `ca-nexus6-hosted-agent` |
| ACA Revision | `ca-nexus6-hosted-agent--0000001` |
| 接続文字列 | `AzureMonitor__ConnectionString` 設定済み |
| 判定 | **Pass（テレメトリ流入あり）** |

## 対象 RunId

| Scenario | RunId |
|---|---|
| S1 | `41aaac6d-fe62-4530-8b3b-f26836d34b5c` |
| S2 | `5acf8500-8656-4f8c-bbdc-ab65f97108a5` |
| S3 | `1630e7a1-65fb-468c-a40b-0f3e92a26171` |

## App Insights クエリ結果

実行形式:

```bash
az monitor app-insights query \
  -g rg-nexus6-swc \
  --app appi-nexus6-swc \
  --analytics-query '<KQL>' \
  -o json
```

### 24 時間テーブル件数

```kusto
union withsource=table requests, traces, dependencies, exceptions
| where timestamp > ago(24h)
| summarize Count=count() by table
| order by table asc
```

| table | Count |
|---|---:|
| dependencies | 511 |
| exceptions | 106 |
| requests | 25 |
| traces | 697 |

`requests` と `traces` が 1 件以上存在するため、ConnectionString 設定済み環境としての最低条件を満たす。

### Workflow 関連ログ件数

```kusto
traces
| where timestamp > ago(24h)
| summarize
    WorkflowStep=countif(message contains "Workflow step"),
    Dequeue=countif(message contains "dequeue"),
    Notification=countif(message contains "notification"),
    DivisionRecommend=countif(message contains "division-recommend")
```

| term | Count |
|---|---:|
| Workflow step | 72 |
| dequeue | 0 |
| notification | 18 |
| division-recommend | 36 |

### Workflow 関連ログサンプル

```kusto
traces
| where timestamp between (datetime(2026-06-27T10:03:00Z) .. datetime(2026-06-27T10:09:00Z))
| where message contains "Workflow step"
   or message contains "dequeue"
   or message contains "notification"
   or message contains "division-recommend"
| project timestamp, severityLevel, message
| order by timestamp asc
| take 20
```

抜粋:

| timestamp | message |
|---|---|
| 2026-06-27T10:03:53.9135602Z | Workflow step web-research started |
| 2026-06-27T10:03:54.0728963Z | Workflow step web-research completed in 159 ms. Succeeded: True |
| 2026-06-27T10:03:54.2147865Z | Workflow step division-recommend-mobile started |
| 2026-06-27T10:03:54.3136286Z | Workflow step division-recommend-fintech completed in 97 ms. Succeeded: True |
| 2026-06-27T10:03:54.3141755Z | Workflow step notification started |
| 2026-06-27T10:03:54.3145347Z | NotificationAgent sending 3 Teams notifications |
| 2026-06-27T10:03:54.9534597Z | Workflow step notification completed in 639 ms. Succeeded: True |
| 2026-06-27T10:06:05.1217496Z | Workflow step web-research started |
| 2026-06-27T10:06:05.3703524Z | Workflow step division-recommend-mobile started |
| 2026-06-27T10:06:05.3716465Z | Workflow step division-recommend-fintech started |

### Foundry / Storage Queue 依存呼び出し

```kusto
dependencies
| where timestamp > ago(24h)
| extend dependencyGroup=case(
    type == "Azure queue" or target contains ".queue.core.windows.net", "Storage Queue",
    target contains "services.ai.azure.com" or name contains "/openai/" or data contains "/openai/", "Foundry/OpenAI",
    "Other")
| where dependencyGroup != "Other"
| summarize Count=count(), Success=countif(success == true), Failure=countif(success == false) by dependencyGroup, resultCode
| order by dependencyGroup asc, resultCode asc
```

| dependencyGroup | resultCode | Count | Success | Failure |
|---|---:|---:|---:|---:|
| Foundry/OpenAI | 400 | 19 | 0 | 19 |
| Storage Queue | 200 | 222 | 222 | 0 |
| Storage Queue | 204 | 3 | 3 | 0 |
| Storage Queue | 403 | 1 | 0 | 1 |

サンプル取得クエリ:

```kusto
dependencies
| where timestamp between (datetime(2026-06-27T10:03:00Z) .. datetime(2026-06-27T10:09:00Z))
| extend dependencyGroup=case(
    type == "Azure queue" or target contains ".queue.core.windows.net", "Storage Queue",
    target contains "services.ai.azure.com" or name contains "/openai/" or data contains "/openai/", "Foundry/OpenAI",
    "Other")
| where dependencyGroup != "Other"
| project timestamp, dependencyGroup, type, target, name, resultCode, success
| order by timestamp asc
| take 30
```

抜粋:

| timestamp | dependencyGroup | target | name | resultCode | success |
|---|---|---|---|---:|---|
| 2026-06-27T10:03:53.9043572Z | Storage Queue | stnexus6skill1t2i.queue.core.windows.net | Azure queue: stnexus6skill1t2i/news-analysis-jobs | 200 | True |
| 2026-06-27T10:03:53.9757551Z | Foundry/OpenAI | fd-partneriq.services.ai.azure.com | POST /openai/deployments/gpt-5.4/chat/completions | 400 | False |
| 2026-06-27T10:03:54.1293891Z | Foundry/OpenAI | fd-partneriq.services.ai.azure.com | POST /openai/deployments/gpt-5.4/chat/completions | 400 | False |
| 2026-06-27T10:03:54.9536440Z | Storage Queue | stnexus6skill1t2i.queue.core.windows.net | Azure queue: stnexus6skill1t2i/news-analysis-jobs | 204 | True |

### RunId ログ検索

S1:

```kusto
traces
| where timestamp > ago(24h)
| where message contains "41aaac6d-fe62-4530-8b3b-f26836d34b5c"
| project timestamp, severityLevel, message
| order by timestamp desc
| take 20
```

S2:

```kusto
traces
| where timestamp > ago(24h)
| where message contains "5acf8500-8656-4f8c-bbdc-ab65f97108a5"
| project timestamp, severityLevel, message
| order by timestamp desc
| take 20
```

S3:

```kusto
traces
| where timestamp > ago(24h)
| where message contains "1630e7a1-65fb-468c-a40b-0f3e92a26171"
| project timestamp, severityLevel, message
| order by timestamp desc
| take 20
```

| Scenario | result rows |
|---|---:|
| S1 | 0 |
| S2 | 0 |
| S3 | 0 |

RunId 文字列は App Insights の `traces.message` には出力されていない。

### requests / exceptions 補足

requests 集計:

```kusto
requests
| where timestamp > ago(24h)
| summarize Count=count() by name, resultCode, success
| order by Count desc
| take 20
```

| name | resultCode | success | Count |
|---|---:|---|---:|
| GET / | 404 | False | 6 |
| GET /devui | 200 | True | 5 |
| GET /devui/logs | 200 | True | 5 |
| GET /health | 200 | True | 4 |
| HEAD /devui | 405 | False | 3 |
| POST /devui/run | 200 | True | 2 |

exceptions サンプル:

```kusto
exceptions
| where timestamp > ago(24h)
| project timestamp, type, outerMessage, problemId
| order by timestamp desc
| take 5
```

| timestamp | type | outerMessage |
|---|---|---|
| 2026-06-27T10:08:17.1997423Z | System.IO.IOException | Access to the path '/app/logs' is denied. |
| 2026-06-27T10:08:17.1130075Z | System.IO.IOException | Access to the path '/app/logs' is denied. |
| 2026-06-27T10:08:16.8947533Z | System.IO.IOException | Access to the path '/app/logs' is denied. |
| 2026-06-27T10:08:16.4318677Z | Microsoft.Data.SqlClient.SqlException | A connection was successfully established with the server, but then an error occurred during the login process. |
| 2026-06-27T10:08:16.4313368Z | Microsoft.Data.SqlClient.SqlException | A connection was successfully established with the server, but then an error occurred during the login process. |

## ACA 設定確認

実行コマンド:

```bash
az containerapp show \
  -g rg-nexus6-swc \
  -n ca-nexus6-hosted-agent \
  --query '{name:name, latestRevision:properties.latestRevisionName, env:properties.template.containers[0].env[?name==`AzureMonitor__ConnectionString` || name==`APPLICATIONINSIGHTS_CONNECTION_STRING` || name==`OTEL_EXPORTER_OTLP_ENDPOINT`]}' \
  -o json
```

結果: `AzureMonitor__ConnectionString` は設定済み。App Insights 側の ConnectionString と ApplicationId が一致している。

## 観測ギャップと将来対応案

- `traces.message` に Workflow RunId が含まれておらず、RunId 直接検索は 0 件。将来対応として、Workflow 開始・完了ログに RunId を含めるか、`customDimensions.RunId` として構造化出力する。
- `dequeue` 文字列を含む trace は 0 件。Queue dependency は記録されているため、将来対応として dequeue 成功時に RunId / QueueMessageId を含む明示ログを出力する。
- Foundry/OpenAI dependency は 400 が 19 件。Phase 3 では Mock fallback で Workflow は成功しているが、将来対応として Foundry 要求パラメータの互換性を修正する。
- `/app/logs` 書き込み権限例外が記録されている。必要なら永続ログ出力先を stdout / Azure Monitor 優先に整理する。

## 結論

App Insights `appi-nexus6-swc` には Phase 3 E2E 実行時間帯の `requests` / `traces` / `dependencies` / `exceptions` が流入している。Workflow step、notification、division-recommend、および Storage Queue / Foundry(OpenAI) 依存呼び出しを確認できたため、`o-observability` は Pass とする。

## 短期運用案（AppTraces 正式運用）

Hosted Agent 実行の一次監視先を `AppTraces` に固定し、`AppGenAIContent` は補助確認とする。理由は、現時点で Hosted Agent の LLM 実行は `AppTraces` には確実に記録される一方、`AppGenAIContent` には web-ag のみが記録される時間帯があるため。

### クエリ1: 実行有無（5分ビン）

```kusto
AppTraces
| where TimeGenerated > ago(6h)
| where Message has "Foundry Assistant run"
| summarize Runs=count() by bin(TimeGenerated, 5m)
| order by TimeGenerated desc
```

判定:

- Runs が 1 以上のビンがあれば、Hosted Agent 側の Foundry 実行は到達している。

### クエリ2: Assistant 内訳（どの Agent が動いたか）

```kusto
AppTraces
| where TimeGenerated > ago(6h)
| where Message has "Foundry Assistant run"
| extend AssistantId = extract("AssistantId=([^;]+);", 1, Message)
| summarize Runs=count(), LastSeen=max(TimeGenerated) by AssistantId
| order by LastSeen desc
```

判定:

- 複数 AssistantId が出ること。
- `LastSeen` が直近実行時刻に追随すること。

### クエリ3: 失敗検知（未完了 run の有無）

```kusto
AppTraces
| where TimeGenerated > ago(6h)
| where Message has_any ("Foundry Assistant run", "invocation failed", "ended with status", "did not complete")
| project TimeGenerated, SeverityLevel, Message
| order by TimeGenerated desc
| take 200
```

判定:

- `completed` が継続して出ること。
- `failed` / `ended with status` / `did not complete` が急増していないこと。

### 補助クエリ: AppGenAIContent 到達確認

```kusto
AppGenAIContent
| where TimeGenerated > ago(6h)
| summarize Count=count(), LastSeen=max(TimeGenerated) by AgentName, ServiceName, RoleName
| order by LastSeen desc
```

補助判定:

- Hosted Agent 名が未表示でも、クエリ1-3が正常なら実行パスは正常とみなす。
- `AppGenAIContent` への統一は中期対応（計測経路の統一）として扱う。

## 中期対応（GenAI セマンティクスで `AppGenAIContent` に統合）

Hosted Agent から GenAI セマンティクスに沿った OpenTelemetry の `Activity` を発行し、`AppGenAIContent` に `AgentName` / `InputMessages` / `OutputMessages` を投入する。

### 実装

- `ActivitySource` 名: `Nexus6.NewsAnalysisAgent.GenAI`
- 発行箇所: `FoundryAssistantsClient.RunAssistantAsync` で run 完了直後
- 発行属性: `gen_ai.system="az.ai.openai"`, `gen_ai.operation.name="chat"`, `gen_ai.request.model` / `gen_ai.response.model` / `gen_ai.response.id` / `gen_ai.agent.id` / `gen_ai.agent.name`
- 発行内容: `gen_ai.input.messages` (developer + user), `gen_ai.output.messages` (assistant)
- 登録: `Program.cs` の `AddOpenTelemetry().UseAzureMonitor(...).WithTracing(t => t.AddSource(GenAITelemetry.SourceName))`

### 確認クエリ

```kusto
AppGenAIContent
| where TimeGenerated > ago(2h)
| summarize Count=count(), LastSeen=max(TimeGenerated) by AgentName, AppRoleName
| order by LastSeen desc
```

判定:

- `AppRoleName` に `ca-nexus6-hosted-agent` が現れ、`AgentName` に WebResearch / Impact / 各 DivisionRecommend 系の名前が並ぶこと。
- `AppTraces`（短期運用案）と件数が概ね対応すること。

