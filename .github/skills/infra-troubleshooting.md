---
name: infra-troubleshooting
description: Azure infrastructure deployment troubleshooting for the EnvironmentSetup CLI. Use when Bicep deployment fails, RBAC errors occur, or region/quota issues arise.
---

# インフラデプロイ トラブルシューティング Skill

## リージョン構成（必須ルール）

リソースごとに使用リージョンが決まっている。**変更禁止**。

| リソース | リージョン | 理由 |
|----------|-----------|------|
| Container Apps, AI Search, Key Vault, Storage, ACR, Monitoring | **northeurope** | swedencentral の AKS/Search 容量不足 |
| AI Foundry (gpt-5, text-embedding-3-large) | **swedencentral** | gpt-5 GlobalStandard は swedencentral/eastus/eastus2 のみ |
| Functions (App Service Plan) | **swedencentral** | northeurope の serverFarms クォータ=0 |
| Fabric Capacity (F4) | **swedencentral** | northeurope のリージョンクォータ=0 |

Bicepパラメータ: `location=northeurope`, `aiFoundryLocation=swedencentral`

## エラー別対処マップ

### `ResourcesForSkuUnavailable` (AI Search)
- basic/standard SKU がリージョンで利用不可
- **対処**: `infra/modules/ai-search.bicep` の SKU を変更するか、リージョンを northeurope にする

### `ManagedEnvironmentCapacityHeavyUsageError` (Container Apps)
- AKS 重負荷
- **対処**: Container Apps のリージョンを northeurope に（`location` パラメータ）

### `InvalidResourceProperties` - gpt-5 GlobalStandard not supported
- gpt-5 は特定リージョンのみ
- **対処**: AI Foundry を swedencentral に（`aiFoundryLocation` パラメータ）。**location ではなく aiFoundryLocation を使う。**

### `InternalSubscriptionIsOverQuotaForSku` - serverFarms
- Functions の App Service Plan クォータ=0
- **対処**: Functions のリージョンを `aiFoundryLocation` に。Step05 で自動フォールバック済み。

### `RegionalQuota: 0` / `CapacityUnits` (Fabric)
- Fabric F4 クォータなし
- **対処**: Fabric を swedencentral にデプロイ。**Fabric はアプリ必須。スキップ禁止。**

### HTTP 401 on Foundry Files API (Step12)
- `Cognitive Services User` ロールの伝播遅延（5-10分）
- **対処1**: Step05 で `deployerObjectId` 経由で Bicep 割り当て（Step12到達時に伝播済み）
- **対処2**: Step12 リトライ: 10回×30秒（最大5分）
- **やってはいけないこと**: 0件アップロードで「完了」にしない。例外をスローする。

### `ResourceNotFound` - RBAC module で AI リソースが見つからない
- RBAC module が依存リソースの作成完了前に実行
- **対処**: `main.bicep` で rbac module に `dependsOn: [aiSearch, aiFoundry, keyVault]`

### Key Vault シークレット書き込み 403
- publicNetworkAccess が Azure に自動で Disabled にされる
- **対処**: Step14 で `az keyvault update --public-network-access Enabled` を実行してから secret set

### `Unsupported value: 'temperature'` (gpt-5)
- gpt-5 は temperature=1 のみ
- **対処**: Chat Completions は temperature 指定しない。Assistants は run 作成時 `temperature=1` でオーバーライド。

### `BadRequest` - max_tokens (gpt-5)
- gpt-5 は `max_tokens` ではなく `max_completion_tokens`
- **対処**: `FoundryAgentClient` で `max_completion_tokens` を使用

### HTTP 429 / `rate_limit_exceeded`
- TPM 上限到達
- **対処**: gpt-5 capacity=200K, リトライ（Chat=3回, Assistants=5回, 線形バックオフ）

### 504 Gateway Timeout (Container Apps)
- リクエスト ~220s でタイムアウト（設定変更不可）
- **対処**: 非同期ワークフロー（POST→202+jobId, GET /status/{id} ポーリング）

### Container App 名が見つからない
- Bicep は suffix 付き名前を生成（例: `ca-nexus6-hosted-agent-7404`）
- **対処**: `deployment.ContainerAppNameAgent` を使う。ハードコード禁止。

## デプロイ前チェックリスト

再デプロイ前に**必ず**実行:

```bash
# 1. RG 完全削除確認
az group list --query "[?contains(name,'nexus6')]"

# 2. ソフト削除 AI Services パージ
az cognitiveservices account list-deleted --query "[?contains(name,'nexus6')]"
az cognitiveservices account purge --name <name> --location swedencentral --resource-group <rg>

# 3. Fabric ワークスペース残存確認・削除
TOKEN=$(az account get-access-token --resource https://api.fabric.microsoft.com --query accessToken -o tsv)
curl -s "https://api.fabric.microsoft.com/v1/workspaces" -H "Authorization: Bearer $TOKEN"

# 4. setup-state.json 削除
rm -f src/EnvironmentSetup/src/EnvironmentSetup.App/setup-state.json
```

## Bicep パラメータ

| パラメータ | デフォルト | 説明 |
|-----------|-----------|------|
| `location` | northeurope | 主要リソース (CA, Search, KV, Storage) |
| `aiFoundryLocation` | swedencentral | AI Foundry, Functions, Fabric |
| `suffix` | (auto) | リソース名 suffix |
| `deployFabric` | false | Fabric デプロイ (Step04 で判定) |
| `deployFunctions` | true | Functions デプロイ (失敗時自動フォールバック) |
| `deployerObjectId` | '' | デプロイ実行ユーザー OID |

## 絶対やってはいけないこと

1. **Fabric をスキップしてデプロイを続行** — アプリのデータレイヤーが動かない
2. **リージョンを単一に統一** — 各リソースに制約があり分散が必要
3. **Container App 名をハードコード** — Bicep は suffix 付きで生成する
4. **Step12 で 0件アップロードを「完了」扱い** — 例外スローが正しい
5. **temperature を gpt-5 に渡す** — 必ず削除またはオーバーライド=1
