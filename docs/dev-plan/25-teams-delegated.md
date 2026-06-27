# 25. Teams Graph Delegated 認証

関連: [24. Teams Graph 通知設計変更](24-teams-graph.md) | [09. Azure / M365 構成](09-azure-m365-configuration.md) | [10. シークレット管理](10-secrets-management.md)

---

## 方針

Track U で `ChannelMessage.Send` は delegated permission のみ有効で、application permission + Managed Identity では通常チャネル投稿が成立しないことを確認した。Track V では Public client App Registration + Device Code Flow で初回ユーザー同意を行い、取得した refresh token を Key Vault に保存する。

Hosted Agent は起動後、Key Vault から `ClientId` / `TenantId` / `RefreshToken` を読み、refresh token grant で delegated access token を取得して Graph に投稿する。新しい refresh token が返った場合は Key Vault に上書きし、ローテーションに追随する。

---

## App Registration

| 項目 | 値 |
|---|---|
| Display name | `nexus6-webpulse-teams-delegated` |
| appId / clientId | `1b6cb046-04fb-4b18-b4cc-45ffff2249c9` |
| objectId | `eaf84dd9-1698-4dda-89c6-9308283c27e8` |
| servicePrincipal objectId | `c971b59c-a538-4c79-89f0-b159e8f47f6c` |
| Tenant | `9e575763-d389-4aa8-b0a9-a64ba4cc1029` |
| Public client | `isFallbackPublicClient=true` |

Delegated permissions:

| Permission | Scope ID | 状態 |
|---|---|---|
| `ChannelMessage.Send` | `ebf0f66e-9fb1-49e4-a278-222f76911cf4` | 追加済み |
| `Group.Read.All` | `5f8c59db-677d-491f-a6b8-5f174b11ec1d` | 追加済み |
| `offline_access` | `7427e0e9-2fba-42fe-b0c0-848c9e6a8182` | 追加済み |

`az ad app permission admin-consent` は CLI 側で既存 service principal 名の重複エラーになったため、`az ad app permission grant --scope 'ChannelMessage.Send Group.Read.All offline_access'` で tenant-wide delegated grant を作成済み。

---

## Device Code 初期手順

初回のみ、管理者または投稿用ユーザーで次を実行する。

```bash
TENANT_ID=9e575763-d389-4aa8-b0a9-a64ba4cc1029
CLIENT_ID=1b6cb046-04fb-4b18-b4cc-45ffff2249c9
SCOPE='https://graph.microsoft.com/ChannelMessage.Send https://graph.microsoft.com/Group.Read.All offline_access'

curl -sS -X POST "https://login.microsoftonline.com/${TENANT_ID}/oauth2/v2.0/devicecode" \
  -H 'Content-Type: application/x-www-form-urlencoded' \
  --data-urlencode "client_id=${CLIENT_ID}" \
  --data-urlencode "scope=${SCOPE}"
```

返却された `verification_uri` と `user_code` でサインインし、`device_code` を token endpoint にポーリングする。`access_token` と `refresh_token` が返ったら、`refresh_token` は表示・保存せず Key Vault に直接登録する。

このセッションでは対話用 `ask_user` が利用できなかったため、Device Code のユーザー操作と `Teams--Graph--RefreshToken` 登録は未完了。

---

## Key Vault シークレット

Key Vault: `kv-nexus6-swc`

| Secret | 値 | 状態 |
|---|---|---|
| `Teams--Graph--ClientId` | App `appId` | 登録済み |
| `Teams--Graph--TenantId` | Tenant ID | 登録済み |
| `Teams--Graph--RefreshToken` | Device Code Flow で取得した refresh token | 未登録（ユーザーサインイン待ち） |

ACA System MI `65c3be76-e682-43d7-b8d4-7a31b9acd1f6` は Key Vault `Key Vault Secrets User` と refresh token rotation 用 `Key Vault Secrets Officer` 付与済み。開発者にも登録作業用に `Key Vault Secrets Officer` を付与した。

---

## 実装

`GraphTeamsPlugin` は application `.default` トークンを使わず、次の順で delegated token を取得する。

1. Key Vault から `Teams--Graph--ClientId` / `Teams--Graph--TenantId` / `Teams--Graph--RefreshToken` を取得。
2. `POST /{tenant}/oauth2/v2.0/token` に `grant_type=refresh_token`、`client_id`、`refresh_token`、Graph delegated scopes を送信。
3. `access_token` で `POST /teams/{teamId}/channels/{channelId}/messages` を実行。
4. token response に新しい `refresh_token` があれば Key Vault に書き戻す。
5. Graph 失敗時は `MockTeamsPlugin` に fallback する。

---

## E2E 状態

| 項目 | 結果 |
|---|---|
| `dotnet test` | 23 tests passed |
| ACA 再デプロイ | `crnexus6swc.azurecr.io/nexus6-hosted-agent:v-teams-delegated-20260627` を `ca-nexus6-hosted-agent--0000008` へデプロイ済み。Running / Healthy |
| `/devui/run` S1 | HTTP 200。refresh token 未登録のため `mock:Mobile` / `mock:Ecommerce` / `mock:Fintech` fallback |
| 3 チャネル実投稿 | 未実施（refresh token 未登録） |
| `GET /messages?$top=1` 確認 | 未実施 |
| ACA Graph POST 201 ログ | 未確認。ACA logs では `Teams Graph delegated credentials are not configured` と mock fallback を確認 |

実投稿未実施理由: Device Code Flow のユーザーサインインが必要だが、この実行環境には `ask_user` がなく、refresh token を取得できなかったため。

---

## 再サインインが必要になる条件

- refresh token が失効または revoke された。
- 長期間未使用（例: 90 日無使用）で refresh token が無効化された。
- Conditional Access / MFA / パスワード変更により追加認証が要求された。
- App Registration の permission または consent が変更された。
- 投稿用ユーザーが対象 Team / Channel から外れた。
