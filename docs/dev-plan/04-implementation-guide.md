# 04. .NET 10 実装ガイド・プロジェクト構成

関連: [開発計画 README](README.md) | [Hosted Agent 仕様](03-hosted-agent-spec.md)

---

## 技術スタック

| 項目 | 採用技術 | バージョン |
|---|---|---|
| ランタイム | .NET | 10 |
| 言語 | C# | 13 |
| Agent Framework | Microsoft Agent Framework for .NET | Preview / 最新安定候補 |
| LLM / Agent 実行基盤 | Azure AI Foundry | Agent Framework Foundry Connector |
| 検索 | Azure AI Search / Bing Search Tool | - |
| HTTP ホスティング | ASP.NET Core Minimal API | .NET 10 |
| 耐障害性 | Microsoft.Extensions.Resilience (Polly v8) | 10.x |
| ログ | Microsoft.Extensions.Logging + Azure Monitor | - |
| DI | Microsoft.Extensions.DependencyInjection | .NET 10 同梱 |
| テスト | xUnit + NSubstitute | - |

---

## NuGet パッケージ

```xml
<!-- src/NewsAnalysisAgent/NewsAnalysisAgent.csproj -->
<ItemGroup>
  <!-- Microsoft Agent Framework for .NET（Preview のため GA 時に確認） -->
  <PackageReference Include="Microsoft.Agents.AI" Version="*" />
  <PackageReference Include="Microsoft.Agents.AI.Foundry" Version="*" />
  <PackageReference Include="Microsoft.Agents.AI.Workflows" Version="*" />
  <PackageReference Include="Microsoft.Agents.AI.Hosting" Version="*" />
  <!-- Azure 認証・データアクセス -->
  <PackageReference Include="Azure.Identity" Version="1.*" />
  <PackageReference Include="Azure.Search.Documents" Version="11.*" />
  <!-- 耐障害性 -->
  <PackageReference Include="Microsoft.Extensions.Resilience" Version="10.*" />
  <!-- Azure Monitor テレメトリ -->
  <PackageReference Include="Azure.Monitor.OpenTelemetry.AspNetCore" Version="1.*" />
</ItemGroup>
```

> Microsoft Agent Framework の .NET パッケージは Preview 前提のため、実装開始時に NuGet / Microsoft Learn の最新パッケージ名と API を確認する。

---

## プロジェクト構成

```
src/
└── news-analysis-agent/
    ├── NewsAnalysisAgent.sln
    ├── src/
    │   ├── NewsAnalysisAgent.Host/           # エントリポイント (ASP.NET Core Minimal API)
    │   │   ├── Program.cs
    │   │   ├── appsettings.json
    │   │   └── appsettings.Development.json
    │   │
    │   ├── NewsAnalysisAgent.Orchestration/  # Hosted Agent 本体
    │   │   ├── NewsAnalysisOrchestrator.cs
    │   │   └── NewsAnalysisContext.cs
    │   │
    │   ├── NewsAnalysisAgent.Agents/         # Agent 1〜4 実装
    │   │   ├── WebResearch/
    │   │   │   ├── WebResearchAgent.cs
    │   │   │   └── WebResearchPrompts.cs
    │   │   ├── BusinessImpact/
    │   │   │   ├── BusinessImpactAgent.cs
    │   │   │   └── BusinessImpactPrompts.cs
    │   │   ├── DivisionRecommend/
    │   │   │   ├── DivisionRecommendAgent.cs
    │   │   │   ├── DivisionRecommendAgentFactory.cs
    │   │   │   └── RecommendPrompts.cs
    │   │   └── Notification/
    │   │       ├── NotificationAgent.cs
    │   │       └── NotificationPrompts.cs
    │   │
    │   ├── NewsAnalysisAgent.Tools/          # Agent Framework Tool 実装
    │   │   ├── BingSearchPlugin.cs
    │   │   ├── FabricDataPlugin.cs
    │   │   ├── MobileDataPlugin.cs
    │   │   ├── EcommerceDataPlugin.cs
    │   │   ├── FintechDataPlugin.cs
    │   │   ├── TeamsNotificationPlugin.cs
    │   │   └── Dynamics365Plugin.cs
    │   │
    │   └── NewsAnalysisAgent.Models/         # 共有モデル
    │       ├── WebResearchResult.cs
    │       ├── BusinessImpactResult.cs
    │       ├── DivisionRecommendation.cs
    │       └── NotificationResult.cs
    │
    └── tests/
        ├── NewsAnalysisAgent.UnitTests/
        └── NewsAnalysisAgent.IntegrationTests/
```

---

## Program.cs 構成例

```csharp
var builder = WebApplication.CreateBuilder(args);

// Azure AI Foundry / Microsoft Agent Framework の接続設定
builder.Services.AddSingleton(_ => new AgentFrameworkOptions
{
    FoundryProjectEndpoint = builder.Configuration["Foundry:ProjectEndpoint"]!,
    DefaultModelDeployment = builder.Configuration["Foundry:DefaultModelDeployment"]!
});

// Tool 登録
builder.Services.AddSingleton<BingSearchPlugin>();
builder.Services.AddSingleton<FabricDataPlugin>();
builder.Services.AddSingleton<MobileDataPlugin>();
builder.Services.AddSingleton<EcommerceDataPlugin>();
builder.Services.AddSingleton<FintechDataPlugin>();
builder.Services.AddSingleton<TeamsNotificationPlugin>();
builder.Services.AddSingleton<Dynamics365Plugin>();

// エージェント・オーケストレーター登録
builder.Services.AddSingleton<WebResearchAgent>();
builder.Services.AddSingleton<BusinessImpactAgent>();
builder.Services.AddSingleton<DivisionRecommendAgentFactory>();
builder.Services.AddSingleton<NotificationAgent>();
builder.Services.AddSingleton<NewsAnalysisOrchestrator>();

// 耐障害性
builder.Services.AddResiliencePipeline("agent-retry", pipeline =>
    pipeline.AddRetry(new RetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        Delay = TimeSpan.FromSeconds(2),
        BackoffType = DelayBackoffType.Exponential
    }));

// Azure Monitor テレメトリ
builder.Services.AddOpenTelemetry().UseAzureMonitor();

var app = builder.Build();

// エンドポイント定義
app.MapPost("/api/analyze", async (
    AnalyzeRequest request,
    NewsAnalysisOrchestrator orchestrator,
    CancellationToken ct) =>
{
    var result = await orchestrator.RunAsync(request.NewsText, ct);
    return Results.Ok(result);
});

app.Run();
```

---

## Agent 基底クラス設計

各 Agent は共通インターフェースを実装する。

```csharp
/// <summary>Microsoft Agent Framework の Agent 呼び出しをラップする基底クラス</summary>
public abstract class AgentBase<TInput, TOutput>
{
    protected readonly IAgentClient _agentClient;
    protected readonly ILogger _logger;

    protected AgentBase(IAgentClient agentClient, string name, string instructions, ILogger logger)
    {
        _agentClient = agentClient;
        _logger = logger;
    }

    public abstract Task<TOutput> RunAsync(TInput input, CancellationToken ct = default);

    /// <summary>エージェントを1ターン実行し、応答テキストを返す</summary>
    protected async Task<string> InvokeAsync(string userMessage, CancellationToken ct)
    {
        var response = await _agentClient.InvokeAsync(userMessage, cancellationToken: ct);
        return response.Text;
    }
}
```

---

## プラグイン実装パターン

Agent Framework の Tool は、LLM から呼び出される関数として薄いラッパーにする。

```csharp
public sealed class BingSearchPlugin(BingConnector connector)
{
    [AgentTool("search_web")]
    [Description("Web 上でキーワード検索を実行し、関連情報のサマリを返す")]
    public async Task<string> SearchAsync(
        [Description("検索クエリ")] string query,
        [Description("取得件数（デフォルト5）")] int count = 5)
    {
        var results = await connector.SearchAsync(query, count);
        return string.Join("\n---\n", results.Select(r => $"{r.Title}\n{r.Snippet}\n{r.Url}"));
    }
}
```

---

## appsettings.json 構成

```json
{
  "Foundry": {
    "ProjectEndpoint": "https://<foundry-resource>.services.ai.azure.com/api/projects/<project-name>",
    "DefaultModelDeployment": "gpt-4o",
    "NotificationModelDeployment": "gpt-4o-mini"
  },
  "AzureAISearch": {
    "Endpoint": "https://<search-resource>.search.windows.net",
    "SkillIndexName": "nexus6-skill-index"
  },
  "BingSearch": {
    "ApiKey": ""
  },
  "Fabric": {
    "WorkspaceId": "",
    "LakehouseId": ""
  },
  "Teams": {
    "MobileWebhookUrl": "",
    "EcommerceWebhookUrl": "",
    "FintechWebhookUrl": ""
  },
  "Dynamics365": {
    "OrganizationUrl": ""
  },
  "AzureMonitor": {
    "ConnectionString": ""
  }
}
```

> **注意**: 本番の機密情報は Azure Key Vault または環境変数で管理し、appsettings には格納しない。

---

## 開発・実行手順

```bash
# ビルド
dotnet build src/news-analysis-agent/NewsAnalysisAgent.sln

# ユニットテスト
dotnet test src/news-analysis-agent/tests/NewsAnalysisAgent.UnitTests/

# 統合テスト（Azure 接続必要）
dotnet test src/news-analysis-agent/tests/NewsAnalysisAgent.IntegrationTests/

# ローカル起動
cd src/news-analysis-agent/src/NewsAnalysisAgent.Host
dotnet run
```
