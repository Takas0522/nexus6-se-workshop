# 10. シークレット管理

関連: [開発計画 README](README.md) | [.NET 10 実装ガイド](04-implementation-guide.md) | [Azure / M365 構成](09-azure-m365-configuration.md) | [Copilot SDK デモデータ生成仕様](../usecase/copilot-sdk-demo-data-generation.md)

---

## 基本方針

- 実シークレット値はリポジトリに置かず、ローカルは `dotnet user-secrets` または環境変数、Azure は Key Vault を使う。
- `src/*/appsettings.Development.json` は `.gitignore` 対象であり、接続文字列や `GITHUB_TOKEN` をコミットしない。
- Container Apps は System Assigned Managed Identity に統一し、アプリコードに Azure API キーやサービスプリンシパルシークレットを持たせない。

---

## GitHub Copilot SDK 認証

DemoDataGenerator では GitHub Copilot SDK の認証トークンを `GitHub:Token` として user-secrets に保存する。

```bash
cd src/DemoDataGenerator
dotnet user-secrets init
dotnet user-secrets set "GitHub:Token" "<token>"
```

`<token>` は Copilot 利用権のある GitHub アカウントで取得する。GitHub CLI 認証済みなら `gh auth token` で現在のトークンを確認できる。PAT を使う場合は有効期限を短くし、SDK 認証ガイドで必要な最小 scope のみに限定する。`repo`、`admin:*`、`org:*` など不要な広域 scope は付与しない。

BYOK を使う場合は GitHub 認証の代替として、Copilot SDK の `Provider` プロパティで OpenAI などのプロバイダーと API キーを指定する。API キーも user-secrets または環境変数で扱い、`appsettings.Development.json` やソースコードには書かない。

---

## Teams Graph 通知設定

Teams 通知は Microsoft Graph delegated permission + Device Code Flow 初期認証で実行する。Graph 投稿先 Team / Channel ID はシークレットではないため ACA 環境変数で設定し、delegated token 更新に必要な値のみ Key Vault に保存する。

| 環境変数 | 対応設定キー | 用途 |
|---|---|---|
| `Teams__Graph__Mobile__TeamId` | `Teams:Graph:Mobile:TeamId` | モバイル事業部 Teams チーム |
| `Teams__Graph__Mobile__ChannelId` | `Teams:Graph:Mobile:ChannelId` | モバイル事業部 `Web Pulse Recommender` チャネル |
| `Teams__Graph__Ecommerce__TeamId` | `Teams:Graph:Ecommerce:TeamId` | EC 事業部 Teams チーム |
| `Teams__Graph__Ecommerce__ChannelId` | `Teams:Graph:Ecommerce:ChannelId` | EC 事業部 `Web Pulse Recommender` チャネル |
| `Teams__Graph__Fintech__TeamId` | `Teams:Graph:Fintech:TeamId` | 金融事業部 Teams チーム |
| `Teams__Graph__Fintech__ChannelId` | `Teams:Graph:Fintech:ChannelId` | 金融事業部 `Web Pulse Recommender` チャネル |

| Key Vault シークレット | 用途 |
|---|---|
| `Teams--Graph--ClientId` | Public client App Registration の appId |
| `Teams--Graph--TenantId` | Entra tenant ID |
| `Teams--Graph--RefreshToken` | Device Code Flow で取得した delegated refresh token |

`Teams--WorkflowsUrl--*` は旧 Workflows 設計用で deprecated。既存値があっても削除せず、現行 `GraphTeamsPlugin` からは参照しない。Graph delegated refresh / POST が失敗した場合は `MockTeamsPlugin` にフォールバックする。

---

## ローカル / Codespaces 認証

ローカル開発と Codespaces では `az login` 済みの Azure CLI 認証を流用する。アプリは `DefaultAzureCredential` を使い、その `AzureCliCredential` チェーンで Key Vault、Fabric、Storage などにアクセスする。

Container Apps では個人認証を使わず、System Assigned Managed Identity に統一する。必要な RBAC は Foundry、Fabric、Key Vault、Storage、Application Insights などの対象リソースに付与する。

---

## コミット禁止物 早見表

| 禁止物 | 理由 |
|---|---|
| `appsettings.Development.json` | ローカル接続文字列・トークンが入るため |
| `.env` / `.env.*` | 環境変数シークレットが入るため |
| `*.pfx` | 証明書秘密鍵を含むため |
| connection string | Fabric / SQL / Storage 接続情報を含むため |
| `GITHUB_TOKEN` | Copilot SDK 認証に使うトークンのため |
| Key Vault シークレット値 | 外部 API キーを含むため |
