using Azure.Core;
using Azure.Identity;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Azure.Security.KeyVault.Secrets;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.Resilience;
using NewsAnalysisAgent.Agents.BusinessImpact;
using NewsAnalysisAgent.Agents.DivisionRecommend;
using NewsAnalysisAgent.Agents.Notification;
using NewsAnalysisAgent.Agents.WebResearch;
using NewsAnalysisAgent.Agents.Infrastructure;
using NewsAnalysisAgent.Agents.Infrastructure.Knowledge;
using NewsAnalysisAgent.Host.Queue;
using NewsAnalysisAgent.Models;
using NewsAnalysisAgent.Orchestration;
using NewsAnalysisAgent.Tools;
using Polly;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(builder.Configuration["Urls"] ?? "http://localhost:5000");

builder.Services.AddSingleton<TokenCredential>(_ => new DefaultAzureCredential());
builder.Services.AddHttpClient("teams-workflows");
builder.Services.AddHttpClient("teams-graph");
builder.Services.AddHttpClient("foundry-agent", client => client.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddHttpClient("bing-grounding", client => client.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddHttpClient<IWebPageFetchPlugin, WebPageFetchPlugin>(client => client.Timeout = TimeSpan.FromSeconds(10))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 3
    });

var keyVaultUri = builder.Configuration["KeyVault:Uri"];
if (!string.IsNullOrWhiteSpace(keyVaultUri))
{
    builder.Services.AddSingleton(sp => new SecretClient(new Uri(keyVaultUri), sp.GetRequiredService<TokenCredential>()));
}

builder.Services.AddSingleton<MockFoundryAgentClient>();
if (!string.IsNullOrWhiteSpace(builder.Configuration["Foundry:ProjectEndpoint"]) &&
    !string.IsNullOrWhiteSpace(builder.Configuration["Foundry:DefaultModelDeployment"]))
{
#pragma warning disable CS0618
    builder.Services.AddSingleton<FoundryAgentClient>();
    builder.Services.AddSingleton<FoundryAssistantsClient>();
    builder.Services.AddSingleton<IFoundryAgentClient>(sp => sp.GetRequiredService<FoundryAgentClient>());
#pragma warning restore CS0618
}
else
{
    builder.Services.AddSingleton<IFoundryAgentClient>(sp => sp.GetRequiredService<MockFoundryAgentClient>());
}
builder.Services.AddSingleton<MockBingSearchPlugin>();
builder.Services.AddSingleton<IBingSearchPlugin, BingSearchPlugin>();
builder.Services.AddSingleton<MockFabricDataPlugin>();
builder.Services.AddSingleton<IFabricDataPlugin, FabricDataPlugin>();
builder.Services.AddSingleton<MockMobileDataPlugin>();
builder.Services.AddSingleton<IMobileDataPlugin, MobileDataPlugin>();
builder.Services.AddSingleton<MockEcommerceDataPlugin>();
builder.Services.AddSingleton<IEcommerceDataPlugin, EcommerceDataPlugin>();
builder.Services.AddSingleton<MockFintechDataPlugin>();
builder.Services.AddSingleton<IFintechDataPlugin, FintechDataPlugin>();
builder.Services.AddSingleton<MockTeamsPlugin>();
builder.Services.AddSingleton<ITeamsNotificationPlugin, GraphTeamsPlugin>();
builder.Services.AddSingleton<IDynamics365Plugin, Dynamics365Plugin>();
builder.Services.AddSingleton<IKnowledgeProvider, LocalFolderKnowledgeProvider>();

builder.Services.AddSingleton<WebResearchAgent>();
builder.Services.AddSingleton(sp => new BusinessImpactAgent(
    (IFoundryAgentClient?)sp.GetService<FoundryAssistantsClient>() ?? sp.GetRequiredService<MockFoundryAgentClient>(),
    sp.GetRequiredService<IFabricDataPlugin>(),
    sp.GetRequiredService<IKnowledgeProvider>(),
    sp.GetRequiredService<ILogger<BusinessImpactAgent>>()));
builder.Services.AddSingleton<IDivisionRecommendAgentFactory>(sp => new DivisionRecommendAgentFactory(
    sp.GetRequiredService<IMobileDataPlugin>(),
    sp.GetRequiredService<IEcommerceDataPlugin>(),
    sp.GetRequiredService<IFintechDataPlugin>(),
    (IFoundryAgentClient?)sp.GetService<FoundryAssistantsClient>() ?? sp.GetRequiredService<MockFoundryAgentClient>(),
    sp.GetRequiredService<IKnowledgeProvider>(),
    sp.GetRequiredService<ILogger<DivisionRecommendAgent>>()));
builder.Services.AddSingleton<NotificationAgent>();
builder.Services.AddSingleton<WorkflowExecutionStore>();
builder.Services.AddSingleton<NewsAnalysisWorkflowBuilder>();
builder.Services.AddSingleton(sp => sp.GetRequiredService<NewsAnalysisWorkflowBuilder>().Build());

builder.Services.AddResiliencePipeline("agent-retry", pipeline =>
{
    pipeline.AddRetry(new Polly.Retry.RetryStrategyOptions
    {
        MaxRetryAttempts = 1,
        Delay = TimeSpan.FromSeconds(2),
        BackoffType = DelayBackoffType.Exponential
    });
});

var monitorConnectionString = builder.Configuration["AzureMonitor:ConnectionString"];
if (!string.IsNullOrWhiteSpace(monitorConnectionString))
{
    builder.Services.AddOpenTelemetry().UseAzureMonitor(options =>
    {
        options.ConnectionString = monitorConnectionString;
    });
}

var storageAccount = builder.Configuration["Storage:Account"];
if (string.IsNullOrWhiteSpace(storageAccount))
{
    builder.Services.AddSingleton<IQueueClientFactory, DisabledQueueClientFactory>();
}
else
{
    builder.Services.AddSingleton(sp => new QueueServiceClient(
        new Uri($"https://{storageAccount}.queue.core.windows.net"),
        sp.GetRequiredService<TokenCredential>(),
        new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 }));
    builder.Services.AddSingleton<IQueueClientFactory, AzureQueueClientFactory>();
}

builder.Services.AddHostedService<QueueBackgroundService>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/devui", (WorkflowExecutionStore store) => Results.Content($$"""
<!doctype html>
<html lang="en">
<head><meta charset="utf-8"><title>News Analysis Agent DevUI</title></head>
<body>
  <h1>News Analysis Agent DevUI</h1>
  <p>Microsoft Agent Framework DevUI placeholder. Replace with MapAgentFrameworkDevUI when Preview APIs stabilize.</p>
  <p>Latest run JSON: <a href="/devui/logs">/devui/logs</a></p>
  <pre>{{System.Net.WebUtility.HtmlEncode(System.Text.Json.JsonSerializer.Serialize(store.LatestRun))}}</pre>
</body>
</html>
""", "text/html"));

app.MapGet("/devui/logs", (WorkflowExecutionStore store) => Results.Ok(new
{
    store.LatestRun,
    store.LatestContext
}));

var manualTriggerEnabled = app.Configuration.GetValue<bool>("DevUi:EnableManualTrigger");
if (manualTriggerEnabled)
{
    app.MapPost("/devui/run", async (ManualRunRequest request, IWorkflow<NewsAnalysisContext> workflow, CancellationToken ct) =>
    {
        if (string.IsNullOrWhiteSpace(request.OriginalNewsText))
        {
            return Results.BadRequest(new { error = "originalNewsText is required." });
        }

        var context = new NewsAnalysisContext
        {
            OriginalNewsText = request.OriginalNewsText,
            SearchHints = request.SearchHints ?? []
        };
        var result = await workflow.RunAsync(context, ct);
        return Results.Ok(result);
    });
}

app.Run();

public sealed record ManualRunRequest(string OriginalNewsText, string[]? SearchHints = null);

public partial class Program;
