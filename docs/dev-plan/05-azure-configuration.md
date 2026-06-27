# 05. Azure 依存サービス構成仕様

関連: [開発計画 README](README.md) | [実装ガイド](04-implementation-guide.md)

---

## 構成サービス一覧

| サービス | 用途 | 必須/任意 |
|---|---|---|
| Azure AI Foundry (Azure OpenAI) | LLM 推論（GPT-4o / gpt-4o-mini）、File Search、Grounding with Bing Search | 必須 |
| ADLS Gen2 | Skill.md ファイル格納・OneLake Shortcut 基盤 | 必須 |
| Microsoft Fabric / OneLake | 全社経営データ参照（Agent 2・3） | 必須 |
| Microsoft Teams (Workflows) | レコメンド通知（Agent 4） | 必須 |
| Azure Container Apps (Consumption) | .NET 10 Hosted Agent ホスティング | 必須 |
| Microsoft Entra ID | Managed Identity / Service Principal | 必須 |
| Azure Monitor / App Insights | テレメトリ・ログ収集 | 推奨 |
| Azure Key Vault | 外部サービスの API キー管理 | 推奨 |
| Dynamics 365 | 本番時の担当者ディレクトリ（Agent 4） | **Demo は使わずモック** |
| Azure AI Search | Skill.md 検索（将来拡張） | Demo では不要 |

> Skill.md / DS.md の参照は Foundry **File Search**（Knowledge）を採用し、Azure AI Search はデモ構成から除外する。実データ量は 100KB 以下と見込まれ File Search で十分。

---

## Azure AI Foundry

### セットアップ

1. Azure ポータルで **Azure AI Foundry** リソースを作成（リージョンは `<既存環境に合わせる>`）
2. `gpt-4o` ・ `gpt-4o-mini` モデルをデプロイ
3. Foundry Project を作成し、Project Endpoint を `appsettings.json` に設定
4. 認証は **Managed Identity に統一**（App / Container Apps の System Assigned MI に `Cognitive Services User` を付与）
5. Skill.md / DS.md 参照用の **File Search**（Knowledge）ベクトルストアを作成し、ADLS Gen2 をデータソースとして接続
6. Web 検索用に **Grounding with Bing Search** 接続を Foundry Project に追加

### 推奨モデル構成

| エージェント | モデル | 最大トークン | 温度 |
|---|---|---|---|
| Agent 1 (Web収集) | gpt-4o | 4,096 | 0.3 |
| Agent 2 (インパクト評価) | gpt-4o | 8,192 | 0.1 |
| Agent 3 (レコメンド) | gpt-4o | 8,192 | 0.2 |
| Agent 4 (通知) | gpt-4o-mini | 2,048 | 0.0 |

> Agent 4 は構造化出力のみのため、コスト効率の良い mini モデルを推奨。

### Microsoft Agent Framework / Foundry との接続

```csharp
// Foundry Project Endpoint へ Managed Identity で接続
services.AddSingleton(_ => new FoundryAgentClient(
    projectEndpoint: config["Foundry:ProjectEndpoint"]!,
    credential: new DefaultAzureCredential()));
```

---

## Grounding with Bing Search（Foundry 組込）

### セットアップ

1. Foundry Portal で **Connections → Grounding with Bing Search** を追加
2. Foundry Project に接続を関連づけ、Connection ID を `Foundry:GroundingBingConnectionId` として `appsettings` に保持
3. Agent 1 の Tool として `GroundingBingSearchTool` を登録

> 独立の Bing Search v7 API は新規受付停止済。Demo / 本番とも Foundry 組込の Grounding with Bing Search に統一する。

---

## Skill.md 参照（Foundry File Search）

### セットアップ

1. ADLS Gen2 (`nexus6skillstore` / `skill-docs` コンテナ) に Skill.md を配置
2. Foundry Project の **Knowledge → File Search** でベクトルストアを作成
3. ADLS Gen2 をデータソースとして追加し、`skill-docs/**` をインデクシング
4. Agent 2 / Agent 3 に `FoundryFileSearchTool` を登録し、ベクトルストア ID を `Foundry:FileSearchVectorStoreId` で参照
5. Skill.md 更新時はファイルを ADLS にアップロード → Foundry が自動再インデクシングするため追加作業不要

> Skill.md は 6 ファイル×5～10KB で合計 約 60KB、DS.md と合わせても 100KB 以下。この規模では Azure AI Search Basic は過剰のため Demo では使用しない。

---

## Microsoft Fabric / OneLake

### セットアップ

1. Fabric ワークスペースに各事業部データを格納した **Lakehouse**（`nexus6-bronze` / `nexus6-silver` / `nexus6-gold`）を作成
2. 接続は Managed Identity を使用し、Container Apps の MI に Fabric ワークスペースの Viewer ロールを付与
3. Gold Lakehouse の **SQL Analytics Endpoint** に T-SQL でアクセス (`Microsoft.Data.SqlClient`)

### テーブル対応表

| データプラグイン | 参照 Lakehouse テーブル |
|---|---|
| `FabricDataPlugin` | `kpi.monthly_revenue`, `kpi.monthly_cost_detail`, `kpi.customer_count`, `kpi.fx_sensitivity` |
| `MobileDataPlugin` | `mobile.contracts`, `mobile.mnp_history`, `mobile.device_costs` |
| `EcommerceDataPlugin` | `ecommerce.inventory`, `ecommerce.orders`, `ecommerce.campaign_reactions` |
| `FintechDataPlugin` | `fintech.fx_positions`, `fintech.loan_balances`, `fintech.credit_reviews` |

### 接続コード例

```csharp
public sealed class FabricDataPlugin(FabricLakehouseClient client)
{
    [AgentTool("get_kpi_summary")]
    [Description("全社 KPI サマリを取得する")]
    public async Task<string> GetKpiSummaryAsync(
        [Description("対象年月 (yyyy-MM)")] string yearMonth)
    {
        var result = await client.QueryAsync(
            $"SELECT * FROM kpi.monthly_revenue WHERE year_month = '{yearMonth}'");
        return result.ToJsonString();
    }
}
```

---

## Microsoft Teams（Workflows）

### セットアップ

1. Teams チャネルで **Workflows**（Power Automate 「チャネルへメッセージを投稿」テンプレート）を追加
2. 起動トリガーは「HTTP 要求を受信したとき」を選択し、Adaptive Card JSON を受け取るスキーマを定義
3. 生成された HTTP URL を Key Vault に格納し、Agent 4 が POST する

> Incoming Webhook コネクターは段階的に廃止予定のため未採用。Demo / 本番とも Workflows に統一する。

### Adaptive Card テンプレート（骨子）

```json
{
  "type": "AdaptiveCard",
  "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
  "version": "1.5",
  "body": [
    { "type": "TextBlock", "text": "📊 ${scenario_title}", "size": "Large", "weight": "Bolder" },
    { "type": "TextBlock", "text": "事業部: ${division}", "weight": "Bolder" },
    { "type": "TextBlock", "text": "${insight_summary}", "wrap": true },
    { "type": "FactSet", "facts": "${recommended_actions}" }
  ]
}
```

---

## Dynamics 365（Demo では使用せずモック）

### 用途

- 本番: 通知先担当者の照会・レコメンドのレコード登録
- **Demo: `MockDirectoryPlugin`（メモリ内の固定担当者一覧）で代替し、実エンティティ/カスタムエンティティは作成しない**

### 将来　実コネクタを入れる際の接続例

```csharp
// Microsoft.PowerPlatform.Dataverse.Client を使用
services.AddSingleton(_ => new ServiceClient(
    instanceUrl: new Uri(config["Dynamics365:OrganizationUrl"]!),
    tokenProviderFunction: GetTokenAsync));
```

---

## Azure Monitor / Application Insights

### 設定

OpenTelemetry 経由で Microsoft Agent Framework / Foundry 呼び出しのトレースを収集する。

```csharp
builder.Services.AddOpenTelemetry()
    .UseAzureMonitor(opt =>
        opt.ConnectionString = config["AzureMonitor:ConnectionString"])
    .WithTracing(tracing => tracing
        .AddSource("Microsoft.Agents*")
        .AddSource("Azure.AI.*"));
```

### 主な監視ポイント

| メトリクス | 説明 |
|---|---|
| `agent.invocation.duration` | 各エージェントの実行時間 |
| `llm.token.usage` | トークン消費量（コスト管理） |
| `agent.error.count` | エージェントエラー件数 |
| `workflow.completion.rate` | ワークフロー完了率 |

---

## Azure Key Vault

Demo / 本番とも、以下の「外部サービスの API キー」のみ Key Vault で管理する。Azure リソース（Foundry / Fabric / Container Apps / Key Vault / Azure Monitor）への認証は **Managed Identity に統一**し、API キーは保持しない。

| シークレット名 | 内容 | 有効性 |
|---|---|---|
| `Teams--WorkflowsUrl` | Teams Workflows の HTTP トリガー URL | Demo 使用 |
| `AzureMonitor--ConnectionString` | App Insights 接続文字列 | Demo 使用（推奨） |
| `Dynamics365--ClientSecret` | Dynamics 365 Service Principal シークレット | 本番のみ |

```csharp
// Key Vault 統合
builder.Configuration.AddAzureKeyVault(
    new Uri($"https://{kvName}.vault.azure.net/"),
    new DefaultAzureCredential());
```
