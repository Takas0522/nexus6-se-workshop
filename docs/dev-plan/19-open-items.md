# 19. 未解決 TODO 集約

関連: [開発計画 README](README.md)

---

| 項目 | 状態 | 現在の代替手段 | 将来対応の優先度 |
|---|---|---|---|
| Bing Grounding 接続 ID | 未設定 | `MockBingSearchPlugin` / 手動 News Portal URL 指定で検証 | 高 |
| Foundry Trigger Agent | 代替解決 | [29 章](29-functions-cron-trigger.md) の Azure Functions Timer Trigger で日次 enqueue を実装済み。Foundry 側 schedule は未設定のままでも運用可 | 中 |
| Teams Graph delegated 初回サインイン | blocked | application 権限方式は不成立のため [25 章](25-teams-delegated.md) に切替済み。Device Code Flow のユーザーサインインと `Teams--Graph--RefreshToken` 登録待ち | 高 |
| Microsoft Agent Framework Preview API 正式採用への置換 | 一部解決 | Agent 2/3 は `FoundryAssistantsClient` で Foundry Assistants file_search に移行済み（[26 章](26-skill-filesearch.md)）。Workflow 境界は独自 `IWorkflow<T>` を継続 | 中 |
| App Insights RunId 構造化ログ | 未整備 | 既存ログと `WorkflowExecutionStore` / DevUI で追跡 | 中 |
| Teams mock fallback ログ | 解決 | ACA では `/home/app/.nexus6/logs/teams-mock` に保存 | 低 |
