# 24. Teams Graph 通知設計変更

関連: [09. Azure / M365 構成](09-azure-m365-configuration.md) | [10. シークレット管理](10-secrets-management.md) | [15. ACA デプロイ](15-aca-deployment.md) | [19. 未解決 TODO](19-open-items.md) | [25. Teams Graph Delegated 認証](25-teams-delegated.md)

---

## 変更理由

Agent 4 の Teams 通知は旧設計では Power Automate Workflows の HTTP URL を Key Vault に保持していた。URL シークレット管理をなくし、Hosted Agent の System Assigned Managed Identity で Microsoft Graph を呼ぶ方式へ変更する。

- Hosted Agent: `ca-nexus6-hosted-agent` (`rg-nexus6-swc`)
- System Assigned MI principalId: `65c3be76-e682-43d7-b8d4-7a31b9acd1f6`
- Tenant: `9e575763-d389-4aa8-b0a9-a64ba4cc1029`

---

## 通知先

| Division | Team ID | Channel ID |
|---|---|---|
| `Ecommerce` | `54e63170-bad8-42b3-959b-cb1cdaad5b6d` | `19:e5c7a76a0e6e400ab94a0c2c1f3052b6@thread.tacv2` |
| `Mobile` | `da1f375e-ab3f-45bd-a8c5-027b1f82a8dc` | `19:8e4127ee9079478d8b9ae900c9d96454@thread.tacv2` |
| `Fintech` | `c0b91401-532b-4092-93d8-1a5850de6027` | `19:1499aad7b2ea4d838495bc731fb8824b@thread.tacv2` |

ACA env vars:

```bash
Teams__Graph__Ecommerce__TeamId=54e63170-bad8-42b3-959b-cb1cdaad5b6d
Teams__Graph__Ecommerce__ChannelId=19:e5c7a76a0e6e400ab94a0c2c1f3052b6@thread.tacv2
Teams__Graph__Mobile__TeamId=da1f375e-ab3f-45bd-a8c5-027b1f82a8dc
Teams__Graph__Mobile__ChannelId=19:8e4127ee9079478d8b9ae900c9d96454@thread.tacv2
Teams__Graph__Fintech__TeamId=c0b91401-532b-4092-93d8-1a5850de6027
Teams__Graph__Fintech__ChannelId=19:1499aad7b2ea4d838495bc731fb8824b@thread.tacv2
```

---

## AppRole 付与手順と実行結果

想定手順:

```bash
GRAPH_SP_ID="$(az ad sp show --id 00000003-0000-0000-c000-000000000000 --query id -o tsv)"
APP_ROLE_ID="$(az ad sp show --id 00000003-0000-0000-c000-000000000000 --query "appRoles[?value=='ChannelMessage.Send'].id" -o tsv)"
MI_OBJECT_ID="65c3be76-e682-43d7-b8d4-7a31b9acd1f6"

az rest --method post \
  --url "https://graph.microsoft.com/v1.0/servicePrincipals/${MI_OBJECT_ID}/appRoleAssignments" \
  --body "{\"principalId\":\"${MI_OBJECT_ID}\",\"resourceId\":\"${GRAPH_SP_ID}\",\"appRoleId\":\"${APP_ROLE_ID}\"}"
```

2026-06-27 実行結果:

- `az account show` の tenantId は `9e575763-d389-4aa8-b0a9-a64ba4cc1029`。
- Graph service principal objectId は `755b35dd-a74a-42e8-a03c-9688ead9f8bd`。
- `appRoles[?value=='ChannelMessage.Send']` は空。
- `oauth2PermissionScopes[?value=='ChannelMessage.Send']` には存在するが `type=User` の delegated permission。
- 実 Graph POST は 403: `Missing role permissions on the request. API requires one of 'Teamwork.Migrate.All'. Roles on the request ''.`

このため、`ChannelMessage.Send` application permission は当該テナントの Graph SP では付与不能。`Teamwork.Migrate.All` は今回の設計方針で採用しないため、実 Teams 投稿は blocked。

管理者確認先: <https://entra.microsoft.com/#view/Microsoft_AAD_IAM/ManagedAppMenuBlade/~/Permissions/appId/00000003-0000-0000-c000-000000000000>

---

## コード変更

| ファイル | 内容 |
|---|---|
| `src/news-analysis-agent/src/NewsAnalysisAgent.Tools/GraphTeamsPlugin.cs` | `DefaultAzureCredential` / Graph `.default` scope でトークン取得し、Teams channel messages API に POST。失敗時のみ `MockTeamsPlugin` に fallback |
| `src/news-analysis-agent/src/NewsAnalysisAgent.Host/Program.cs` | DI を `ITeamsNotificationPlugin -> GraphTeamsPlugin` に切替、`teams-graph` HttpClient 登録 |
| `src/news-analysis-agent/src/NewsAnalysisAgent.Host/appsettings.json` | `Teams:Graph:<Division>:TeamId/ChannelId` を追加。`Teams:WorkflowsUrl:*` は deprecated として残置 |
| `src/news-analysis-agent/src/NewsAnalysisAgent.Tools/LocalLogPath.cs` | ACA 非 root 実行時の mock fallback ログを `/home/app/.nexus6` 配下へ保存 |
| `src/news-analysis-agent/tests/NewsAnalysisAgent.UnitTests/Notification/NotificationAgentTests.cs` | Graph POST と fallback の単体テスト追加 |

---

## ビルド・デプロイ・E2E 結果

| 項目 | 結果 |
|---|---|
| `dotnet test` | 23 tests passed |
| ACR build | `crnexus6swc.azurecr.io/nexus6-hosted-agent:u-teams-graph-20260627-2` / digest `sha256:98f41b6a692fda9d1ea2b8c786bf5fbc4c6bf851e32fe987fc0c88bba9221c34` |
| ACA revision | `ca-nexus6-hosted-agent--0000007` Running / Healthy |
| `/devui` | 200 |
| `/devui/run` | 200、結果保存: `logs/u-teams-graph-devui-run-2.json` |
| Graph POST | 3 division とも 403（権限 blocked） |
| fallback | `mock:Mobile`, `mock:Ecommerce`, `mock:Fintech` 成功。ACA logs に `/home/app/.nexus6/logs/teams-mock/...` 保存を確認 |
| 実 Teams 投稿 | 未達。AppRole 不在のため blocked |

---

## 残課題

1. Microsoft Graph 側で Managed Identity に付与可能な `ChannelMessage.Send` application permission が提供されるか確認する。
2. もし Graph が引き続き `Teamwork.Migrate.All` を要求する場合、今回方針（`Teamwork.Migrate.All` 不採用）との設計再判断が必要。
3. 実投稿が可能になった後、`GET /teams/{teamId}/channels/{channelId}/messages?$top=1` で 3 チャネルの直近メッセージ確認を実施する。
4. 403 が解消しない場合、対象 MI をチーム member に追加する要否も併せて確認する。
---

## Track V への切替

2026-06-27 に application permission 方式を中止し、[25 章](25-teams-delegated.md) の Delegated + Device Code Flow 方式へ切り替えた。新 App Registration `nexus6-webpulse-teams-delegated` (`appId=1b6cb046-04fb-4b18-b4cc-45ffff2249c9`) を作成し、`ChannelMessage.Send` / `Group.Read.All` / `offline_access` の delegated grant を付与済み。Hosted Agent は Key Vault の refresh token を使って delegated access token を更新し、Graph channel messages API に投稿する。

