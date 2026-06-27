# 04. .NET 10 実装ガイド・プロジェクト構成

関連: [開発計画 README](README.md) | [Hosted Agent 仕様](03-hosted-agent-spec.md)

---

## 技術スタック

| 項目 | 採用技術 | バージョン |
|---|---|---|
| ランタイム | .NET | 10 (GA) |
| 言語 | C# | 13 |
| Agent Framework | Microsoft Agent Framework for .NET（Workflow + DevUI） | Preview 最新 |
| LLM / Agent 実行基盤 | Azure AI Foundry（Agent Framework Foundry Connector） | - |
| Skill.md 検索 | Foundry **File Search**（Knowledge） | Foundry 組込 |
| Web 検索 | Foundry **Grounding with Bing Search** | Foundry 組込 |
| HTTP ホスティング | ASP.NET Core Minimal API | .NET 10 |
| デプロイ先 | Azure Container Apps（Consumption） | - |
| 耐障害性 | Microsoft.Extensions.Resilience (Polly v8) | 10.x |
| ログ | Microsoft.Extensions.Logging + Azure Monitor | - |
| 認証 | Azure.Identity の Managed Identity（`DefaultAzureCredential`） | 1.x |
| DI | Microsoft.Extensions.DependencyInjection | .NET 10 同梱 |
| テスト | xUnit + NSubstitute | - |

---

## NuGet パッケージ

```xml
<!-- src/news-analysis-agent/src/NewsAnalysisAgent.Host/NewsAnalysisAgent.Host.csproj -->
<ItemGroup>
  <!-- Microsoft Agent Framework for .NET（Preview 最新を採用） -->
  <PackageReference Include="Microsoft.Agents.AI" Version="*" />
  <PackageReference Include="Microsoft.Agents.AI.Foundry" Version="*" />
  <PackageReference Include="Microsoft.Agents.AI.Workflows" Version="*" />
  <PackageReference Include="Microsoft.Agents.AI.Hosting" Version="*" />
  <PackageReference Include="Microsoft.Agents.AI.DevUI" Version="*" />
  <!-- Azure 認証・データアクセス -->
  <PackageReference Include="Azure.Identity" Version="1.*" />
  <PackageReference Include="Microsoft.Data.SqlClient" Version="5.*" /> <!-- Fabric SQL Endpoint -->
  <!-- 耐障害性 -->
  <PackageReference Include="Microsoft.Extensions.Resilience" Version="*" />
  <!-- Azure Monitor テレメトリ -->
  <PackageReference Include="Azure.Monitor.OpenTelemetry.AspNetCore" Version="1.*" />
</ItemGroup>
```

> Microsoft Agent Framework / DevUI 関連の .NET パッケージ名は Preview 中に変更される可能性がある。実装開始時に NuGet / Microsoft Learn / Agent Framework リポジトリの最新パッケージ名と API を確認すること。

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

// Azure AI Foundry / Microsoft Agent Framework の接続設定（Managed Identity）
builder.Services.AddSingleton(_ => new AgentFrameworkOptions
{
    FoundryProjectEndpoint = builder.Configuration["Foundry:ProjectEndpoint"]!,
    DefaultModelDeployment = builder.Configuration["Foundry:DefaultModelDeployment"]!,
    NotificationModelDeployment = builder.Configuration["Foundry:NotificationModelDeployment"]!
});
builder.Services.AddSingleton<TokenCredential>(_ => new DefaultAzureCredential());

// Tool 登録
builder.Services.AddSingleton<WebPageFetchPlugin>();
builder.Services.AddSingleton<FabricDataPlugin>();
builder.Services.AddSingleton<MobileDataPlugin>();
builder.Services.AddSingleton<EcommerceDataPlugin>();
builder.Services.AddSingleton<FintechDataPlugin>();
builder.Services.AddSingleton<TeamsWorkflowsPlugin>();
builder.Services.AddSingleton<MockDirectoryPlugin>();

// エージェント・Workflow ビルダー登録
builder.Services.AddSingleton<WebResearchAgent>();
builder.Services.AddSingleton<BusinessImpactAgent>();
builder.Services.AddSingleton<DivisionRecommendAgentFactory>();
builder.Services.AddSingleton<NotificationAgent>();
builder.Services.AddSingleton<NewsAnalysisWorkflowBuilder>();
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<NewsAnalysisWorkflowBuilder>().Build());

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

// REST エンドポイント
app.MapPost("/api/analyze", async (
    AnalyzeRequest request,
    Workflow<NewsAnalysisContext, NewsAnalysisContext> workflow,
    CancellationToken ct) =>
{
    var initialContext = new NewsAnalysisContext { OriginalNewsText = request.NewsText };
    var result = await workflow.RunAsync(initialContext, ct);
    return Results.Ok(result);
});

// DevUI マウント（Demo / 開発時にワークフローを可視化）
app.MapAgentFrameworkDevUI("/devui");

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

Agent Framework の Tool は、LLM から呼び出される関数として薄いラッパーにする。Web 検索は Foundry 組込の Grounding with Bing Search を使うため独自実装は不要。Fabric データ参照の例:

```csharp
public sealed class FabricDataPlugin(SqlConnection connection)
{
    [AgentTool("get_monthly_revenue")]
    [Description("事業部別の月次売上・粗利・為替エクスポージャーを取得する")]
    public async Task<string> GetMonthlyRevenueAsync(
        [Description("対象年月 (yyyy-MM)")] string yearMonth,
        CancellationToken ct = default)
    {
        const string sql = "SELECT * FROM kpi.monthly_revenue WHERE year_month = @ym";
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@ym", yearMonth);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await SerializeToJsonAsync(reader, ct);
    }
}
```

---

## appsettings.json 構成

```json
{
  "Foundry": {
    "ProjectEndpoint": "https://fd-pathneriq.services.ai.azure.com/api/projects/<要確認: プロジェクト名>",
    "DefaultModelDeployment": "gpt-4o",
    "NotificationModelDeployment": "gpt-4o-mini",
    "FileSearchVectorStoreId": "<vector-store-id>",
    "GroundingBingConnectionId": "<bing-grounding-connection-id>"
  },
  "Fabric": {
    "SqlEndpoint": "fabric_seworkshop_ws1.datawarehouse.fabric.microsoft.com",
    "Database": "<要確認: Gold Lakehouse 名>"
  },
  "Teams": {
    "WorkflowsUrl": "<Power Automate Workflow の HTTP トリガー URL>"
  },
  "AzureMonitor": {
    "ConnectionString": ""
  }
}
```

> Demo は Managed Identity を採用するため API キー類は appsettings に保持しない。外部サービスで API キーが必要なもののみ Azure Key Vault に格納し、`DefaultAzureCredential` で取得する。

---

## 開発・実行手順

```bash
# ビルド
dotnet build src/news-analysis-agent/NewsAnalysisAgent.sln

# ユニットテスト
dotnet test src/news-analysis-agent/tests/NewsAnalysisAgent.UnitTests/

# 統合テスト（Azure 接続必要 / 任意）
dotnet test src/news-analysis-agent/tests/NewsAnalysisAgent.IntegrationTests/

# ローカル起動（Minimal API + DevUI）
cd src/news-analysis-agent/src/NewsAnalysisAgent.Host
dotnet run
# REST API:  POST http://localhost:5000/api/analyze
# DevUI:    http://localhost:5000/devui
```

## デプロイ（Azure Container Apps / Consumption）

```bash
# コンテナイメージを ACR または GHCR に push 後、Container Apps へデプロイ
az containerapp up \
  --resource-group <要確認: リソースグループ名> \
  --name <要確認: Container Apps アプリ名> \
  --image <registry>/nexus6-hosted-agent:latest \
  --environment <要確認: Container Apps 環境名> \
  --ingress external --target-port 8080 \
  --system-assigned
```

> Container Apps の System Assigned Managed Identity を Foundry / Fabric / Key Vault への RBAC 付与対象とする。レジストリは ACR を必須とせず、GitHub Container Registry でも可。
