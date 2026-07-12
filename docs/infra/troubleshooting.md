# インフラ・デプロイ トラブルシューティング

このドキュメントは EnvironmentSetup CLI の Bicep デプロイ (Step05) および後続ステップで発生した既知の問題と対策をまとめたものです。

---

## 1. リージョン・キャパシティ制約

### 1.1 AI Search SKU 利用不可

| 項目 | 内容 |
|------|------|
| エラー | `ResourcesForSkuUnavailable` - The region 'X' currently does not have enough resources |
| 発生リージョン | swedencentral (standard/basic とも), northeurope (状況による) |
| 対策 | `infra/modules/ai-search.bicep` の SKU を `basic` に設定済み。northeurope に配置。 |
| 関連コミット | `caa4cea` |

### 1.2 Container Apps (AKS) キャパシティ不足

| 項目 | 内容 |
|------|------|
| エラー | `ManagedEnvironmentCapacityHeavyUsageError` - AKS is experiencing heavy usage |
| 発生リージョン | swedencentral |
| 対策 | Container Apps を northeurope にデプロイ。 |
| 関連コミット | `72e5930` |

### 1.3 gpt-5 GlobalStandard リージョン非対応

| 項目 | 内容 |
|------|------|
| エラー | `InvalidResourceProperties` - SKU 'GlobalStandard' for model 'gpt-5' is not supported in 'northeurope' |
| 対応リージョン | swedencentral, eastus, eastus2 |
| 対策 | `aiFoundryLocation` パラメータを追加。AI Foundry は swedencentral 固定。 |
| 関連コミット | `6380ba9` |

### 1.4 Functions (App Service Plan) クォータ=0

| 項目 | 内容 |
|------|------|
| エラー | `InternalSubscriptionIsOverQuotaForSku` - serverFarms |
| 発生リージョン | northeurope (サブスクリプション固有) |
| 対策 | Functions を swedencentral (`aiFoundryLocation`) にデプロイ。失敗時は自動フォールバック (`deployFunctions=false`)。 |
| 関連コミット | `4691dad`, `93b5bb5` |

### 1.5 Fabric Capacity クォータ=0

| 項目 | 内容 |
|------|------|
| エラー | `BadRequest` - RegionalQuota: 0, RequestedSku: F4 |
| 発生リージョン | northeurope |
| 対策 | Fabric Capacity を swedencentral (`aiFoundryLocation`) にデプロイ。**Fabricはアプリ必須のためスキップ不可。** |
| 関連コミット | `aa3d8bf` |

### 1.6 text-embedding-3-large Standard SKU クォータ不足

| 項目 | 内容 |
|------|------|
| エラー | quota exceeded for Standard SKU |
| 対策 | SKU を `GlobalStandard` に変更。GlobalStandard はリージョン横断で共有クォータ。 |
| 関連コミット | `3f74087` |

---

## 2. 現在のリージョン構成

| リソース | リージョン | 理由 |
|----------|-----------|------|
| Container Apps, AI Search, Key Vault, Storage, ACR, Monitoring | northeurope | swedencentral AKS/Search 容量不足 |
| AI Foundry (gpt-5, text-embedding-3-large) | swedencentral | gpt-5 GlobalStandard 対応リージョン |
| Functions | swedencentral | northeurope の Web/serverFarms クォータ=0 |
| Fabric Capacity | swedencentral | northeurope のリージョンクォータ=0 |

**重要:** `main.bicep` で `location` (northeurope) と `aiFoundryLocation` (swedencentral) を分離。

---

## 3. RBAC・認証関連

### 3.1 Step12 Files API 401 (RBAC 伝播遅延)

| 項目 | 内容 |
|------|------|
| エラー | HTTP 401 on Foundry Files API (`/openai/files`) |
| 原因 | `Cognitive Services User` ロールの伝播に 5-10 分かかる |
| 対策1 | Step05 Bicep で `deployerObjectId` を使いデプロイ時にロール割り当て（Step12 到達時には伝播済み） |
| 対策2 | Step12 のリトライ: 10回×30秒 = 最大5分待機 |
| 関連コミット | `8c03640`, `bac4a0c` |

### 3.2 RBAC module の dependsOn 不足

| 項目 | 内容 |
|------|------|
| エラー | `ResourceNotFound` - AI Search/AI Services が RBAC 割り当て前に存在しない |
| 原因 | RBAC module が AI リソースの作成完了を待たずに実行 |
| 対策 | `main.bicep` の rbac module に `dependsOn: [aiSearch, aiFoundry, keyVault]` を追加 |
| 関連コミット | `caa4cea` |

### 3.3 Key Vault パブリックアクセス無効化

| 項目 | 内容 |
|------|------|
| エラー | Key Vault シークレット書き込み失敗 (403) |
| 原因 | Bicep で `publicNetworkAccess: 'Enabled'` を指定しても Azure が自動で Disabled に変更 |
| 対策 | Step14 で `az keyvault update --public-network-access Enabled` を実行してから secret set |
| 関連コミット | `04fe0a6` |

---

## 4. AI モデル制約

### 4.1 gpt-5 temperature 非対応

| 項目 | 内容 |
|------|------|
| エラー | `Unsupported value: 'temperature' does not support X` |
| 原因 | gpt-5 は temperature=1 のみ対応 |
| 対策 | Chat Completions: temperature パラメータ削除。Assistants: run 作成時に `temperature=1` をオーバーライド（file_search が自動で0.3を設定するため）。 |
| 関連コミット | `f20ae6d` |

### 4.2 gpt-5 max_tokens 非対応

| 項目 | 内容 |
|------|------|
| エラー | `BadRequest` on Chat Completions |
| 原因 | gpt-5 は `max_tokens` ではなく `max_completion_tokens` を要求 |
| 対策 | `FoundryAgentClient` で `max_completion_tokens` を使用 |

### 4.3 Rate Limit (429 / rate_limit_exceeded)

| 項目 | 内容 |
|------|------|
| エラー | HTTP 429 / Assistants run status=failed with `rate_limit_exceeded` |
| 原因 | gpt-5 TPM 上限に達する（並列ワークフローで合計 250-400s の推論） |
| 対策 | gpt-5 capacity を 200K TPM に設定。リトライロジック: Chat=3回、Assistants=5回（線形バックオフ 5/10/15/20s）。 |
| 関連コミット | `e9bff41`, `e5b164f`, `7469ec4` |

---

## 5. Container Apps 制約

### 5.1 リクエストタイムアウト (~220s)

| 項目 | 内容 |
|------|------|
| エラー | 504 Gateway Timeout (stream timeout) |
| 原因 | Container Apps のリクエストタイムアウトは ~220s で設定変更不可 |
| 対策 | 非同期ワークフローパターン: POST → 202 + jobId、GET /status/{id} でポーリング。フロントエンド: 3秒間隔ポーリング。 |
| 関連コミット | `ff064cc` |

---

## 6. Step13 (App Deploy) 関連

### 6.1 Container App 名のハードコード

| 項目 | 内容 |
|------|------|
| エラー | Container App が見つからない |
| 原因 | Bicep は suffix 付き名前（例: `ca-nexus6-hosted-agent-7404`）を生成するが、コード側でハードコードしていた |
| 対策 | `deployment.ContainerAppNameAgent` を使用（Step05 で Bicep 出力から取得） |
| 関連コミット | `9e2d303` |

### 6.2 Function App settings のクォート不足

| 項目 | 内容 |
|------|------|
| エラー | `'int' object is not iterable` / 値にスペースが含まれる設定が壊れる |
| 対策 | az CLI 引数を `"key=value"` でクォート |
| 関連コミット | `04fe0a6` |

### 6.3 Assistants RunMaxWaitSeconds タイムアウト

| 項目 | 内容 |
|------|------|
| エラー | Run がタイムアウトで失敗 |
| 原因 | デフォルト 60s では gpt-5 の推論時間に不足 |
| 対策 | `RunMaxWaitSeconds=180` に設定 |
| 関連コミット | `7469ec4` |

---

## 7. デプロイ前チェックリスト

再デプロイ前に確認すべき事項:

1. **ソフト削除されたAI Services のパージ**
   ```bash
   az cognitiveservices account list-deleted --query "[?contains(name,'nexus6')]"
   az cognitiveservices account purge --name <name> --location swedencentral --resource-group <rg>
   ```

2. **Fabric ワークスペースの残存**
   ```bash
   TOKEN=$(az account get-access-token --resource https://api.fabric.microsoft.com --query accessToken -o tsv)
   curl -s "https://api.fabric.microsoft.com/v1/workspaces" -H "Authorization: Bearer $TOKEN"
   ```

3. **setup-state.json の削除**
   ```bash
   rm -f src/EnvironmentSetup/src/EnvironmentSetup.App/setup-state.json
   ```

4. **RG の完全削除確認**
   ```bash
   az group list --query "[?contains(name,'nexus6')]"
   ```

---

## 8. Bicep パラメータ早見表

| パラメータ | デフォルト | 説明 |
|-----------|-----------|------|
| `location` | northeurope | 主要リソースのリージョン |
| `aiFoundryLocation` | swedencentral | AI Foundry / Functions / Fabric のリージョン |
| `suffix` | (auto) | リソース名 suffix (ソフトデリート衝突防止) |
| `deployFabric` | false | Fabric Capacity デプロイ |
| `deployFunctions` | true | Functions デプロイ |
| `deployerObjectId` | '' | デプロイ実行ユーザーの OID (RBAC用) |
| `fabricCapacitySku` | F4 | Fabric SKU |
| `fabricAdminMembers` | [] | Fabric 管理者 UPN リスト |
