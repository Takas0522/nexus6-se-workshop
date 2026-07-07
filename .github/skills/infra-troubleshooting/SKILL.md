---
name: infra-troubleshooting
description: 'Troubleshooting guide for Azure infrastructure deployment in this repository. Use when Bicep deployment fails, RBAC 401 errors occur, region or quota issues arise, AI model errors happen, Container Apps timeout, or when cleaning up and redeploying resources. Covers EnvironmentSetup CLI Steps 5-14.'
---

# Azure インフラデプロイ トラブルシューティング

EnvironmentSetup CLI (Step05 Bicep デプロイ〜Step14) で発生する既知の問題と対処法。

## When to Use This Skill

- Bicep デプロイ (Step05) が失敗した
- RBAC 関連の 401/403 エラーが発生した
- リージョンやクォータ不足のエラーが出た
- AI モデル (gpt-5) のエラーが出た
- Container Apps がタイムアウトした
- 環境を削除して再デプロイしたい

## Gotchas

- **リージョンを単一に統一してはいけない** — 各リソースに制約があり分散必須（下記構成表参照）
- **Fabric をスキップしてデプロイ続行してはいけない** — アプリのデータレイヤー（メダリオン/オントロジー）が動かなくなる
- **Container App 名をハードコードしてはいけない** — Bicep は suffix 付きで生成する（例: `ca-nexus6-hosted-agent-7404`）。`deployment.ContainerAppNameAgent` を使う
- **Step12 で 0件アップロードを「完了」扱いにしてはいけない** — 例外スローが正しい動作
- **gpt-5 に temperature を渡してはいけない** — temperature=1 のみ対応。指定するなら1、しないなら省略
- **gpt-5 に max_tokens を使ってはいけない** — `max_completion_tokens` を使う
- **northeurope に AI Foundry / Functions / Fabric を配置してはいけない** — クォータ=0 または非対応

## リージョン構成（変更禁止）

| リソース | リージョン | Bicep パラメータ |
|----------|-----------|-----------------|
| Container Apps, AI Search, Key Vault, Storage, ACR, Monitoring | **northeurope** | `location` |
| AI Foundry (gpt-5, text-embedding-3-large) | **swedencentral** | `aiFoundryLocation` |
| Functions (App Service Plan) | **swedencentral** | `aiFoundryLocation` |
| Fabric Capacity (F4) | **swedencentral** | `aiFoundryLocation` |

## Troubleshooting

| エラー | 原因 | 対処 |
|--------|------|------|
| `ResourcesForSkuUnavailable` (AI Search) | リージョンで SKU 利用不可 | AI Search は northeurope + basic SKU |
| `ManagedEnvironmentCapacityHeavyUsageError` | swedencentral AKS 過負荷 | Container Apps は northeurope |
| `InvalidResourceProperties` gpt-5 GlobalStandard not supported | northeurope 非対応 | AI Foundry は `aiFoundryLocation=swedencentral` |
| `InternalSubscriptionIsOverQuotaForSku` serverFarms | northeurope Functions クォータ=0 | Functions を `aiFoundryLocation` に。自動フォールバック済み |
| Fabric Lakehouse 作成失敗 (Capacity Inactive) | Bicep デプロイ後 Capacity が Suspended/Inactive | Step07 で自動 Resume 実装済み。ARM API: `POST .../capacities/{name}/resume` |
| HTTP 401 on Foundry Files API (Step12) | Cognitive Services User ロール伝播遅延 (5-10分) | Step12 開始時に GET /files プローブで RBAC 伝播を確認（20回×30秒=最大10分）。失敗時は Azure Portal IAM 確認 |
| `ResourceNotFound` in RBAC module | 依存リソース未作成 | rbac module に `dependsOn: [aiSearch, aiFoundry, keyVault]` |
| Key Vault secret set 403 | publicNetworkAccess 自動無効化 | `az keyvault update --public-network-access Enabled` してから set |
| `Unsupported value: 'temperature'` | gpt-5 は temperature=1 のみ | temperature 省略。Assistants は run で `temperature=1` オーバーライド |
| `BadRequest` max_tokens | gpt-5 非対応 | `max_completion_tokens` を使う |
| HTTP 429 / `rate_limit_exceeded` | TPM 上限 | gpt-5 capacity=200K。リトライ: Chat 3回, Assistants 5回 (線形バックオフ) |
| 504 Gateway Timeout | Container Apps ~220s タイムアウト (変更不可) | 非同期パターン: POST→202+jobId, GET /status ポーリング |
| Container App not found | 名前ハードコード | `deployment.ContainerAppNameAgent` を使う |
| text-embedding-3-large quota exceeded | Standard SKU クォータ不足 | `GlobalStandard` SKU に変更 |

## Step-by-Step Workflows

### 再デプロイ前クリーンアップ

1. RG 完全削除確認: `az group list --query "[?contains(name,'nexus6')]"`
2. ソフト削除 AI Services パージ: `az cognitiveservices account list-deleted --query "[?contains(name,'nexus6')]"` → `az cognitiveservices account purge --name <name> --location swedencentral --resource-group <rg>`
3. Fabric ワークスペース削除: Fabric API で `DELETE /v1/workspaces/{id}`
4. setup-state.json 削除: `rm -f src/EnvironmentSetup/src/EnvironmentSetup.App/setup-state.json`

### Bicep パラメータ一覧

| パラメータ | デフォルト | 説明 |
|-----------|-----------|------|
| `location` | northeurope | 主要リソース |
| `aiFoundryLocation` | swedencentral | AI Foundry / Functions / Fabric |
| `suffix` | (auto) | リソース名 suffix |
| `deployFabric` | false | Fabric デプロイ (Step04 で判定) |
| `deployFunctions` | true | Functions デプロイ (自動フォールバック) |
| `deployerObjectId` | '' | デプロイ実行ユーザー OID (RBAC用) |

## References

- [docs/infra/troubleshooting.md](../../../docs/infra/troubleshooting.md) — 詳細版トラブルシューティング
- [infra/main.bicep](../../../infra/main.bicep) — メインBicepテンプレート
- [infra/main.bicepparam](../../../infra/main.bicepparam) — パラメータファイル
