# EnvironmentSetup CLI 設計書

> **目的**: 業務領域のアセスメントからAzureリソースデプロイ、データ投入、アプリデプロイまでを一気通貫で実行するCLIツール  
> **ランタイム**: .NET 10 + ConsoleAppFramework v5  
> **AI統合**: GitHub Copilot SDK (NuGet: GitHub.Copilot.SDK)  
> **ステータス**: 設計中

---

## 1. アーキテクチャ概要

```
┌─────────────────────────────────────────────────────────┐
│  EnvironmentSetup CLI                                   │
│  ┌───────────┐  ┌──────────────┐  ┌─────────────────┐  │
│  │ Program.cs│→ │ StepRunner   │→ │ Step01..Step10   │  │
│  │ (CAF)     │  │ (順次実行)    │  │ (各ステップ)     │  │
│  └───────────┘  └──────────────┘  └─────────────────┘  │
│        │                │                   │           │
│  ┌─────▼─────┐  ┌──────▼──────┐  ┌────────▼────────┐  │
│  │StateManager│  │CopilotService│  │ AzureCliWrapper │  │
│  │(JSON永続化)│  │(SDK Client)  │  │ (az/bicep呼出) │  │
│  └───────────┘  └─────────────┘  └─────────────────┘  │
└─────────────────────────────────────────────────────────┘
         │                │                    │
         ▼                ▼                    ▼
   setup-state.json   Copilot CLI         Azure Resource Manager
                      (JSON-RPC)
```

---

## 2. プロジェクト構成

```
src/EnvironmentSetup/
├── EnvironmentSetup.sln
└── src/
    └── EnvironmentSetup.App/
        ├── EnvironmentSetup.App.csproj
        ├── Program.cs                    # エントリポイント (ConsoleAppFramework)
        ├── Steps/
        │   ├── ISetupStep.cs             # ステップ共通インターフェース
        │   ├── Step01Assessment.cs       # アセスメント入力
        │   ├── Step02Analysis.cs         # 業務分析 (Copilot SDK)
        │   ├── Step03Report.cs           # レポート作成 (Copilot SDK)
        │   ├── Step04AzureLogin.cs       # Azure ログイン
        │   ├── Step05BicepDeploy.cs      # Bicep デプロイ
        │   ├── Step06DataCreation.cs     # データ生成・投入 (Copilot SDK)
        │   ├── Step07NewsSite.cs         # ニュースサイト生成 (Copilot SDK)
        │   ├── Step08SkillDsMd.cs        # Skill/DS.md 作成 (Copilot SDK)
        │   ├── Step09AppDeploy.cs        # アプリデプロイ
        │   └── Step10EntraId.cs          # Entra ID アプリ作成
        ├── Models/
        │   ├── SetupState.cs             # 全体状態モデル
        │   ├── AssessmentInput.cs        # ステップ1入力
        │   ├── AnalysisResult.cs         # ステップ2出力
        │   └── ReportOutput.cs           # ステップ3出力
        └── Services/
            ├── CopilotService.cs         # Copilot SDK ラッパー
            ├── StateManager.cs           # JSON 永続化
            └── AzureCliWrapper.cs        # az CLI ラッパー
```

---

## 3. 依存パッケージ

| パッケージ | バージョン | 用途 |
|-----------|-----------|------|
| ConsoleAppFramework | 5.* | CLI フレームワーク |
| GitHub.Copilot.SDK | latest | AI生成 (分析/レポート/データ/ニュース/Skill) |
| System.Text.Json | 10.* | JSON シリアライズ |

---

## 4. CLI インターフェース

```bash
# 全ステップ実行
dotnet run --project src/EnvironmentSetup/src/EnvironmentSetup.App

# ステップ5から再開
dotnet run --project src/EnvironmentSetup/src/EnvironmentSetup.App -- --step 5

# ヘルプ
dotnet run --project src/EnvironmentSetup/src/EnvironmentSetup.App -- --help
```

### オプション

| オプション | 型 | デフォルト | 説明 |
|-----------|---|-----------|------|
| `--step` | int | 1 | 開始ステップ番号 (1-10) |
| `--state-file` | string | `./setup-state.json` | 状態ファイルパス |
| `--log-dir` | string | `./logs` | ログ出力先 |
| `--use-defaults` | bool | false | 全入力をデフォルト値で自動実行 |

---

## 5. 状態モデル (SetupState)

```json
{
  "currentStep": 3,
  "completedSteps": [1, 2],
  "assessment": {
    "domains": ["モバイル通信", "Eコマース", "フィンテック"],
    "employeeCount": 5000,
    "notificationTeam": "PartnerIQ-Alerts",
    "notificationChannels": ["mobile-alerts", "ecommerce-alerts", "fintech-alerts"]
  },
  "analysis": {
    "tables": [...],
    "estimatedDataCounts": {...},
    "newsScenarios": [...]
  },
  "report": {
    "generatedAt": "2026-07-01T12:00:00Z",
    "approved": true,
    "outputPath": "./output/report.md"
  },
  "azure": {
    "subscriptionId": "xxx",
    "resourceGroup": "rg-nexus6-xxx",
    "region": "swedencentral"
  },
  "deployment": {
    "storageAccountName": "...",
    "foundryEndpoint": "...",
    "fabricSqlEndpoint": "...",
    "containerAppUrl": "...",
    "entraAppId": "..."
  }
}
```

---

## 6. ステップ詳細設計

### Step 01: アセスメント

**入力項目**:
| # | 項目 | デフォルト値 | バリデーション |
|---|------|------------|--------------|
| 1 | 業務領域①  | モバイル通信 | 空文字不可 |
| 2 | 業務領域② | Eコマース | 空文字不可 |
| 3 | 業務領域③ | フィンテック | 空文字不可 |
| 4 | 従業員数 | 5000 | 正の整数 |
| 5 | 通知チーム名 | PartnerIQ-Alerts | 空文字不可 |
| 6 | 通知チャネル① | mobile-alerts | 空文字不可 |
| 7 | 通知チャネル② | ecommerce-alerts | 空文字不可 |
| 8 | 通知チャネル③ | fintech-alerts | 空文字不可 |

**動作フロー**:
1. 各項目をコンソールプロンプトで表示（デフォルト値を `[default]` で表示）
2. Enter でデフォルト適用、入力でオーバーライド
3. 全入力後に確認表示 → y/n

---

### Step 02: 分析

**Copilot SDK 使用方法**:
```csharp
var session = await client.CreateSessionAsync(new SessionConfig {
    Model = "gpt-5",
    SystemMessage = "あなたはビジネスシステム分析の専門家です。..."
});
await session.SendAsync(new MessageOptions {
    Prompt = $"以下の業務領域のアプリケーション構成を分析してください: {domains}..."
});
```

**入力コンテキスト**:
- アセスメント結果
- 既存 `docs/scenario/` のシナリオ文書
- 既存 `docs/dev-plan/01-overview.md` のシステム概要
- 既存 `fabric/sql/*.sql` のテーブルスキーマ

**出力**:
- テーブル定義一覧（既存スキーマをベースにカスタマイズ）
- 想定ニュースシナリオ（3件）
- 各領域の通知内容テンプレート
- データ量見積もり

---

### Step 03: レポート作成

**Copilot SDK 使用方法**:
- Step02の分析結果を入力としてレポート生成
- マークダウン形式で出力

**レポート構成**:
```markdown
# 環境構築レポート

## テーブル設計
| DB名 | テーブル | 想定行数 |
|------|---------|---------|
| sqldb_common_01 | unified_customers | 15,000 |
| ... | ... | ... |

## 想定ニュース
1. [為替急変] 円が一時158円台に急落...
2. [競合統合] 大手3キャリア×ECプラットフォーム...
3. [金融政策] 日銀が政策金利を0.5%に引き上げ...

## 通知内容サンプル
### モバイル通信 × 為替急変
[Adaptive Card JSON / テキスト]

## データ数サマリ
- 顧客数: 15,000 (5000名 × 3)
- 月間トランザクション合計: 900,000 (6ヶ月分)
  - モバイル: 30,000/月 (顧客×2)
  - EC: 75,000/月 (顧客×5)
  - Fintech: 45,000/月 (顧客×3)
```

**続行確認**: レポート表示後 `続行しますか? (y/n):` で待機

---

### Step 04: Azure ログイン

**フロー**:
1. `az account show` で既存セッション確認
2. 有効 → サブスクリプション表示 + 「このまま続行しますか? (y/n)」
3. 無効 → `az login --use-device-code` 実行
4. ログイン後にサブスクリプション情報を状態保存

---

### Step 05: Bicep デプロイ

**テンプレート構成** (infra/):
```
infra/
├── main.bicep
├── main.bicepparam
└── modules/
    ├── monitoring.bicep          # Log Analytics + App Insights
    ├── keyvault.bicep            # Key Vault
    ├── container-registry.bicep  # ACR
    ├── container-apps-env.bicep  # Container Apps Environment
    ├── container-app.bicep       # Container App
    ├── storage.bicep             # Storage Account ×2
    ├── ai-foundry.bicep          # AI Services + Project + Models
    ├── ai-search.bicep           # AI Search
    ├── functions.bicep           # App Service Plan + Function App
    └── rbac.bicep                # RBAC ロール割り当て
```

**デプロイフロー**:
1. パラメータ生成（アセスメント + 分析結果から）
2. `az deployment group what-if` 実行・表示
3. 続行確認 (y/n)
4. `az deployment group create` 実行
5. 出力値（エンドポイント等）を状態保存

**リソース一覧** (docs/infra/resource-review.md 準拠):
- Log Analytics Workspace
- Application Insights
- Key Vault (RBAC認可)
- Container Registry (Basic)
- Container Apps Environment
- Container App (Hosted Agent)
- Storage Account ×2 (skills用 + portal用)
- AI Services (Foundry) + Project + Model Deployments
- AI Search (Standard)
- App Service Plan (Y1) + Function App

---

### Step 06: データ作成

**Copilot SDK 使用方法**:
- Step03レポートのテーブル定義とデータ量を入力
- 各テーブルのリアルなデモデータをJSON/CSVで生成
- 既存 DemoDataGenerator のスキーマ (`fabric/sql/*.sql`) に合わせた形式

**投入フロー**:
1. Copilot SDK でデータ生成指示
2. 生成データを一時ファイルに保存
3. Fabric SQL に Azure AD Default 認証で接続
4. INSERT 文またはバルクインサート実行
5. 接続文字列テンプレート: `Server={endpoint};Database={dbname};Authentication=Active Directory Default`

**データスケール** (従業員5000名の場合):
- 顧客: 15,000人 (5000×3)
- モバイル契約: 30,000/月 × 6ヶ月 = 180,000件
- EC注文: 75,000/月 × 6ヶ月 = 450,000件
- Fintech取引: 45,000/月 × 6ヶ月 = 270,000件

---

### Step 07: ニュースサイト作成

**Copilot SDK 使用方法**:
- 既存 `src/news-portal/` のHTMLテンプレートを参照
- Step03で想定した3件のニュース記事を生成
- 業務領域に合わせた見出し・本文・カテゴリを生成

**出力ファイル**:
```
output/news-portal/
├── index.html
├── article-1.html
├── article-2.html
├── article-3.html
├── sitemap.xml
└── robots.txt
```

**デプロイ**: Step05で作成したStorage Account の `$web` コンテナにアップロード

---

### Step 08: Skill/DS.md 作成

**Copilot SDK 使用方法**:
- 既存 `docs/skills/skill-manifest.json` の9スキル構成を参照
- 入力された業務領域名でスキル名・内容をカスタマイズ
- Fabricオントロジー用 DS.md とAgent データマッピング用 DS.md を生成

**出力**:
```
output/skills/
├── {domain1}_skill_scenario1.md
├── {domain1}_skill_scenario2.md
├── {domain1}_skill_scenario3.md
├── {domain2}_skill_scenario1.md
├── ...
├── {domain3}_skill_scenario3.md
└── ds/
    ├── fabric-ontology.ds.md
    └── agent-data-mapping.ds.md
```

**参照元**:
- `.github/fabric-skills/` の構成
- `.github/azure-skills/` の構成
- `docs/skills/skill-manifest.json`

---

### Step 09: アプリデプロイ

**対象**:
1. news-analysis-agent → Container App
2. news-trigger-function → Azure Functions

**Container App デプロイフロー**:
1. `az acr build` で ACR にイメージ push
2. `az containerapp update` でイメージ更新
3. 環境変数設定（Foundry endpoint, Fabric SQL, Teams等）

**Functions デプロイフロー**:
1. `func azure functionapp publish {appName}`
2. アプリ設定の更新

---

### Step 10: Entra ID アプリ作成

**構成**:
- アプリ種別: Single-tenant
- 認証フロー: Authorization Code (delegated)
- 権限スコープ (管理者同意不要):
  - `User.Read`
  - `ChannelMessage.Send`
  - `Team.ReadBasic.All`
- Client Secret: 有効期限6ヶ月

**フロー**:
1. `az ad app create` でアプリ登録
2. `az ad app permission add` でスコープ追加
3. `az ad app credential reset` でSecret生成
4. Secret を Key Vault に保存 (`teams-app-client-secret`)
5. Container App の環境変数に AppId/TenantId 設定

---

## 7. Copilot SDK 統合パターン

```csharp
public class CopilotService : IAsyncDisposable
{
    private CopilotClient? _client;
    private CopilotSession? _session;

    public async Task InitializeAsync()
    {
        _client = new CopilotClient();
        await _client.StartAsync();
        _session = await _client.CreateSessionAsync(new SessionConfig
        {
            Model = "gpt-5",
            OnPermissionRequest = PermissionHandler.ApproveAll
        });
    }

    public async Task<string> GenerateAsync(string systemMessage, string prompt)
    {
        // セッションにメッセージ送信し、応答を待機
        var done = new TaskCompletionSource<string>();
        var content = "";
        _session!.On<SessionEvent>(evt =>
        {
            if (evt is AssistantMessageEvent msg)
                content = msg.Data.Content;
            else if (evt is SessionIdleEvent)
                done.SetResult(content);
        });
        await _session.SendAsync(new MessageOptions { Prompt = prompt });
        return await done.Task;
    }

    public async ValueTask DisposeAsync()
    {
        if (_session != null) await _session.DisposeAsync();
        if (_client != null) await _client.StopAsync();
    }
}
```

---

## 8. エラーハンドリング方針

| 状況 | 対応 |
|------|------|
| Copilot SDK接続失敗 | リトライ3回 → 手動テンプレートにフォールバック |
| az CLI 未インストール | エラーメッセージ + インストール手順表示 |
| Bicep デプロイ失敗 | エラー詳細表示 + 再試行/スキップ選択 |
| Fabric SQL 接続失敗 | 接続文字列確認促進 + 再試行 |
| Entra アプリ作成失敗 | 権限不足の場合は手動手順を出力 |

---

## 9. GitHub Copilot SDK プラグイン連携

CLIは `.github/fabric-skills/` と `.github/azure-skills/` のスキルをプラグインとして参照する。

**Fabric Skills** (`.github/fabric-skills/`):
- FabricIQ MCP サーバー経由でFabric管理操作
- Lakehouse作成、テーブル定義、データパイプライン設定

**Azure Skills** (`.github/azure-skills/`):
- Azure MCP サーバー経由でリソース操作
- Entra アプリ登録、Key Vault操作、Container App設定

**統合方法**:
```csharp
var session = await client.CreateSessionAsync(new SessionConfig
{
    Model = "gpt-5",
    Tools = [
        // カスタムツールでaz CLI / fabric操作を公開
        CopilotTool.DefineTool(async (string command) => {
            return await RunAzCliAsync(command);
        }, factoryOptions: new AIFunctionFactoryOptions {
            Name = "az_cli",
            Description = "Execute Azure CLI commands"
        }),
    ],
    OnPermissionRequest = PermissionHandler.ApproveAll
});
```

---

## 10. 出力ディレクトリ構成

```
./output/                          # CLI実行時の出力先
├── report.md                      # Step03 レポート
├── news-portal/                   # Step07 生成ニュースサイト
│   ├── index.html
│   └── article-*.html
├── skills/                        # Step08 生成Skill
│   ├── *.md
│   └── ds/
│       ├── fabric-ontology.ds.md
│       └── agent-data-mapping.ds.md
└── data/                          # Step06 生成データ (一時)
    └── *.json

./setup-state.json                 # 状態永続化
./logs/                            # 実行ログ
    └── setup-{timestamp}.log
```

---

## 11. 前提条件・必須ツール

| ツール | バージョン | 用途 |
|--------|-----------|------|
| .NET SDK | 10.0+ | ビルド・実行 |
| Azure CLI | 2.60+ | Azureリソース管理 |
| Bicep CLI | 0.25+ | IaC テンプレート |
| GitHub Copilot CLI | latest | Copilot SDK runtime |
| func (Azure Functions Core Tools) | 4.x | Functions デプロイ |

---

## 12. 次のステップ

1. ✅ 設計レビュー完了後、プロジェクトスキャフォールドを作成
2. Models / Services の実装
3. 各Stepの実装（Step01から順次）
4. infra/ Bicep テンプレート作成
5. 結合テスト
