# 19. 未解決 TODO 集約

関連: [開発計画 README](README.md)

---

| 項目 | 状態 | 現在の代替手段 | 将来対応の優先度 |
|---|---|---|---|
| Bing Grounding 接続 ID | 未設定 | `MockBingSearchPlugin` / 手動 News Portal URL 指定で検証 | 高 |
| Foundry Trigger Agent | 手動引継ぎ | [16 章](16-foundry-trigger-wiring.md) の手動 enqueue 手順 | 高 |
| Teams Graph delegated 初回サインイン | blocked | application 権限方式は不成立のため [25 章](25-teams-delegated.md) に切替済み。Device Code Flow のユーザーサインインと `Teams--Graph--RefreshToken` 登録待ち | 高 |
| Microsoft Agent Framework Preview API 正式採用への置換 | 未置換 | 独自 `IFoundryAgentClient` / `IWorkflow<T>` プレースホルダ | 中 |
| App Insights RunId 構造化ログ | 未整備 | 既存ログと `WorkflowExecutionStore` / DevUI で追跡 | 中 |
| Teams mock fallback ログ | 解決 | ACA では `/home/app/.nexus6/logs/teams-mock` に保存 | 低 |
