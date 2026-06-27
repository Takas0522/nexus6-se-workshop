# 05. Azure 依存サービス構成仕様

関連: [開発計画 README](README.md) | [実装ガイド](04-implementation-guide.md)

---

## 構成サービス一覧

| サービス | 用途 | 必須/任意 |
|---|---|---|
| Azure AI Foundry (Azure OpenAI) | LLM 推論（GPT-4o） | 必須 |
| Azure AI Search | Skill.md の RAG インデックス | 必須 |
| ADLS Gen2 | Skill.md ファイル格納・OneLake Shortcut | 必須 |
| Bing Search API / Grounding with Bing Search | Web 情報収集（Agent 1） | 必須 |
| Microsoft Fabric / OneLake | 全社経営データ参照（Agent 2） | 必須 |
| Dynamics 365 | 通知先担当者情報（Agent 4） | 必須 |
| Microsoft Teams (Webhook) | レコメンド通知（Agent 4） | 必須 |
| Azure Monitor / App Insights | テレメトリ・ログ収集 | 推奨 |
| Azure Key Vault | シークレット管理 | 推奨 |

---

## Azure AI Foundry

### セットアップ

1. Azure ポータルで **Azure AI Foundry** リソースを作成
2. `gpt-4o` モデルをデプロイ（デプロイ名を `appsettings.json` に設定）
3. Foundry Project Endpoint と認証方式（Managed Identity または API キー）を設定
4. Skill.md 用の Azure AI Search インデックス（`nexus6-skill-index`）を接続

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
// Foundry Project Endpoint へ Managed Identity で接続する想定
services.AddSingleton(_ => new FoundryAgentClient(
    projectEndpoint: config["Foundry:ProjectEndpoint"]!,
    credential: new DefaultAzureCredential()));
```

---

## Bing Search API / Grounding with Bing Search

### セットアップ

1. Foundry の Grounding with Bing Search または Bing Search API を有効化
2. API キーまたは接続情報を Key Vault に格納
3. `BingSearchPlugin` / `WebSearchTool` に注入

```csharp
services.AddSingleton(_ => new BingSearchClient(config["BingSearch:ApiKey"]!));
services.AddSingleton<WebSearchTool>();
```

---

## Microsoft Fabric / OneLake

### セットアップ

1. Fabric ワークスペースに各事業部データを格納した **Lakehouse** を作成
2. OneLake への接続には **Azure Managed Identity** または **Service Principal** を使用
3. Lakehouse SQL Analytics Endpoint で T-SQL 形式のクエリを実行

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

## Microsoft Teams (Webhook)

### セットアップ

1. Teams チャネルで **Incoming Webhook** コネクタを設定
2. Webhook URL を Key Vault に格納
3. Agent 4 が Adaptive Card 形式で POST

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

## Dynamics 365

### 用途

- 通知先担当者（事業部責任者）の照会
- レコメンド内容のレコード登録（Next Action 管理）

### 接続

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

本番環境では以下のシークレットを Key Vault で管理する。

| シークレット名 | 内容 |
|---|---|
| `AzureOpenAI--ApiKey` | Azure OpenAI API キー |
| `BingSearch--ApiKey` | Bing Search API キー |
| `AzureAISearch--ApiKey` | Azure AI Search API キー |
| `Teams--Mobile--WebhookUrl` | Teams Mobile チャネル Webhook |
| `Teams--Ecommerce--WebhookUrl` | Teams EC チャネル Webhook |
| `Teams--Fintech--WebhookUrl` | Teams Fintech チャネル Webhook |
| `Dynamics365--ClientSecret` | Dynamics 365 認証シークレット |
| `AzureMonitor--ConnectionString` | App Insights 接続文字列 |

```csharp
// Key Vault 統合（.NET 10 推奨パターン）
builder.Configuration.AddAzureKeyVault(
    new Uri($"https://{kvName}.vault.azure.net/"),
    new DefaultAzureCredential());
```
