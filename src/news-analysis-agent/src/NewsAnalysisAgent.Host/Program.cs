using System.Text;
using Azure.Core;
using Azure.Identity;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Azure.Security.KeyVault.Secrets;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.Options;
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

// --- Blob-based domain configuration loading ---
var domainConfigBlobUri = builder.Configuration["DomainConfig:BlobUri"];
var domainConfigLoaded = false;
if (!string.IsNullOrWhiteSpace(domainConfigBlobUri))
{
    try
    {
        var credential = new DefaultAzureCredential();
        var blobClient = new BlobClient(new Uri(domainConfigBlobUri), credential);
        var downloadResult = await blobClient.DownloadContentAsync();
        var configJson = downloadResult.Value.Content.ToString();
        var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson));
        builder.Configuration.AddJsonStream(memoryStream);
        domainConfigLoaded = true;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️ DomainConfig Blob load failed ({ex.Message}). Trying local fallback...");
    }
}

// Fallback: load local domain-config.json if blob load failed
if (!domainConfigLoaded)
{
    var localConfigPath = Path.Combine(AppContext.BaseDirectory, "domain-config.json");
    if (File.Exists(localConfigPath))
    {
        builder.Configuration.AddJsonFile(localConfigPath, optional: true);
        Console.WriteLine($"✓ DomainConfig loaded from local file: {localConfigPath}");
    }
}
builder.Services.Configure<DivisionsConfig>(builder.Configuration.GetSection("Divisions").Exists()
    ? builder.Configuration
    : new ConfigurationBuilder().AddJsonStream(
        new MemoryStream(Encoding.UTF8.GetBytes("{\"Divisions\":[]}"))).Build());

// Bind DivisionsConfig from the "Divisions" section or root
builder.Services.Configure<DivisionsConfig>(options =>
{
    var divisionsSection = builder.Configuration.GetSection("Divisions");
    if (divisionsSection.Exists())
    {
        var divisions = divisionsSection.Get<List<DivisionConfig>>() ?? [];
        options.Divisions = divisions;
    }
});

builder.Services.AddSingleton<TokenCredential>(_ => new DefaultAzureCredential());
builder.Services.AddHttpClient("teams-workflows");
builder.Services.AddHttpClient("teams-graph");
builder.Services.AddHttpClient("foundry-agent", client => client.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddHttpClient("bing-grounding", client => client.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddHttpClient("webiq", client => client.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddHttpClient<WebPageFetchPlugin>(client => client.Timeout = TimeSpan.FromSeconds(10))
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
builder.Services.AddSingleton<MockFabricDataPlugin>();

// WebIQ or legacy Bing+WebPageFetch
var webIqBaseUrl = builder.Configuration["WebIq:BaseUrl"];
if (!string.IsNullOrWhiteSpace(webIqBaseUrl))
{
  builder.Services.AddSingleton<WebPageFetchPlugin>();
  builder.Services.AddSingleton(sp =>
  {
      // API Key を Key Vault から取得
      var secretClient = sp.GetService<SecretClient>();
      var secretName = builder.Configuration["WebIq:KeyVaultSecretName"] ?? "webiq-api-key";
      string apiKey;
      if (secretClient is not null)
      {
          try
          {
              apiKey = secretClient.GetSecret(secretName).Value.Value;
          }
          catch (Exception ex)
          {
              sp.GetRequiredService<ILogger<WebIqPlugin>>()
                .LogWarning(ex, "Failed to retrieve WebIQ API key from Key Vault. WebIQ will fallback.");
              apiKey = "";
          }
      }
      else
      {
          apiKey = builder.Configuration["WebIq:ApiKey"] ?? "";
      }

      return new WebIqPlugin(
          webIqBaseUrl,
          apiKey,
          sp.GetRequiredService<IHttpClientFactory>(),
          sp.GetRequiredService<MockBingSearchPlugin>(),
          sp.GetRequiredService<WebPageFetchPlugin>(),
          sp.GetRequiredService<ILogger<WebIqPlugin>>());
  });
  builder.Services.AddSingleton<IBingSearchPlugin>(sp => sp.GetRequiredService<WebIqPlugin>());
  builder.Services.AddSingleton<IWebPageFetchPlugin>(sp => sp.GetRequiredService<WebIqPlugin>());
}
else
{
  builder.Services.AddSingleton<IBingSearchPlugin, BingSearchPlugin>();
  builder.Services.AddSingleton<WebPageFetchPlugin>();
  builder.Services.AddSingleton<IWebPageFetchPlugin>(sp => sp.GetRequiredService<WebPageFetchPlugin>());
}

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

builder.Services.AddSingleton(sp => new WebResearchAgent(
  (IFoundryAgentClient?)sp.GetService<FoundryAssistantsClient>() ?? sp.GetRequiredService<IFoundryAgentClient>(),
  sp.GetRequiredService<IBingSearchPlugin>(),
  sp.GetRequiredService<IWebPageFetchPlugin>(),
  sp.GetRequiredService<ILogger<WebResearchAgent>>()));
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

// Config-driven division support
builder.Services.AddSingleton<IConfigDrivenRecommendAgentFactory, ConfigDrivenRecommendAgentFactory>();
builder.Services.AddSingleton<ConfigDrivenWorkflowBuilder>();
builder.Services.AddSingleton(sp =>
{
    // Register GenericDivisionDataPlugins from DivisionsConfig
    var config = sp.GetRequiredService<IOptions<DivisionsConfig>>().Value;
    var appConfig = sp.GetRequiredService<IConfiguration>();
    var logger = sp.GetRequiredService<ILogger<GenericDivisionDataPlugin>>();
    return config.Divisions
        .Select(d => (IDivisionDataPlugin)new GenericDivisionDataPlugin(d, appConfig, logger))
        .ToList();
});
builder.Services.AddSingleton<IEnumerable<IDivisionDataPlugin>>(sp =>
    sp.GetRequiredService<List<IDivisionDataPlugin>>());

// Workflow: use config-driven builder if divisions are configured, otherwise legacy
builder.Services.AddSingleton(sp =>
{
    var configBuilder = sp.GetRequiredService<ConfigDrivenWorkflowBuilder>();
    if (configBuilder.HasDivisions)
    {
        return configBuilder.Build();
    }
    return sp.GetRequiredService<NewsAnalysisWorkflowBuilder>().Build();
});

builder.Services.AddResiliencePipeline("agent-retry", pipeline =>
{
  pipeline.AddRetry(new Polly.Retry.RetryStrategyOptions
  {
    MaxRetryAttempts = 1,
    Delay = TimeSpan.FromSeconds(2),
    BackoffType = DelayBackoffType.Exponential
  });
});

var monitorConnectionString =
    builder.Configuration["AzureMonitor:ConnectionString"] ??
    builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
if (!string.IsNullOrWhiteSpace(monitorConnectionString))
{
  builder.Services.AddOpenTelemetry()
    .UseAzureMonitor(options =>
    {
      options.ConnectionString = monitorConnectionString;
      options.SamplingRatio = 1.0f;
    })
    .WithTracing(tracing => tracing.AddSource(GenAITelemetry.SourceName));
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
  <p>Latest run JSON: <a href="/devui/logs">/devui/logs</a></p>
  {{RenderDevUi(store.LatestRun, store.LatestContext)}}
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

public partial class Program
{
  private static string RenderDevUi(WorkflowRunLog? run, NewsAnalysisContext? context)
  {
    var html = new StringBuilder();
    html.Append("""
<style>
  :root { color-scheme: light; }
  body { font-family: Inter, "Segoe UI", Arial, sans-serif; margin: 0; background: #f5f7fb; color: #182230; }
  .page { max-width: 1280px; margin: 0 auto; padding: 24px; }
  .hero {
    background: linear-gradient(135deg, #0f172a 0%, #1d4ed8 60%, #2563eb 100%);
    color: white; border-radius: 20px; padding: 24px 28px; box-shadow: 0 12px 40px rgba(15, 23, 42, .18);
    margin-bottom: 20px;
  }
  .hero h1 { margin: 0 0 8px; font-size: 28px; }
  .hero p { margin: 0; opacity: .9; }
  .grid { display: grid; grid-template-columns: 1.15fr .85fr; gap: 18px; }
  .card {
    background: #fff; border: 1px solid #e5eaf3; border-radius: 18px; padding: 18px 20px;
    box-shadow: 0 8px 28px rgba(15, 23, 42, .06);
  }
  .card h2 { margin: 0 0 14px; font-size: 18px; }
  .workflow { display: flex; flex-wrap: wrap; gap: 10px; align-items: stretch; }
  .step {
    min-width: 132px; border-radius: 14px; padding: 12px 14px; border: 1px solid #dbe4f0;
    background: linear-gradient(180deg, #ffffff 0%, #f8fbff 100%);
    box-shadow: inset 0 1px 0 rgba(255,255,255,.8);
  }
  .step.primary { background: linear-gradient(180deg, #eff6ff 0%, #dbeafe 100%); border-color: #bfdbfe; }
  .step .label { font-size: 12px; letter-spacing: .04em; text-transform: uppercase; color: #64748b; }
  .step .name { margin-top: 4px; font-weight: 700; }
  .arrow { align-self: center; color: #94a3b8; font-size: 18px; font-weight: 700; }
  .chip-row { display: flex; flex-wrap: wrap; gap: 8px; margin-top: 10px; }
  .chip {
    display: inline-flex; align-items: center; gap: 6px; padding: 6px 10px; border-radius: 999px;
    background: #eef2ff; color: #3730a3; border: 1px solid #c7d2fe; font-size: 12px; font-weight: 600;
  }
  .metric-grid { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 12px; }
  .metric {
    background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 14px; padding: 12px 14px;
  }
  .metric .k { font-size: 12px; color: #64748b; margin-bottom: 8px; }
  .metric .v { font-size: 18px; font-weight: 800; color: #0f172a; }
  .metric .s { font-size: 12px; color: #94a3b8; margin-top: 4px; }
  table { width: 100%; border-collapse: collapse; }
  th, td { padding: 10px 12px; border-bottom: 1px solid #e5e7eb; vertical-align: top; text-align: left; }
  th { background: #f8fafc; color: #475569; font-size: 12px; text-transform: uppercase; letter-spacing: .03em; }
  tbody tr:hover { background: #fbfdff; }
  .status-ok { color: #166534; background: #dcfce7; padding: 4px 8px; border-radius: 999px; font-size: 12px; font-weight: 700; }
  .status-ng { color: #991b1b; background: #fee2e2; padding: 4px 8px; border-radius: 999px; font-size: 12px; font-weight: 700; }
  .muted { color: #64748b; }
  .empty {
    padding: 18px; border: 1px dashed #cbd5e1; border-radius: 14px; color: #64748b; background: #f8fafc;
  }
</style>
<div class="page">
  <div class="hero">
    <h1>News Analysis Agent DevUI</h1>
    <p>Workflow の進行状況、最新実行、参照データをまとめて確認できます。</p>
  </div>
""");
    html.Append("""
<div class="card" style="margin-bottom:18px">
  <h2>Workflow</h2>
  <div class="workflow">
    <div class="step primary"><div class="label">Step 1</div><div class="name">Web Research</div></div>
    <div class="arrow">→</div>
    <div class="step primary"><div class="label">Step 2</div><div class="name">Business Impact</div></div>
    <div class="arrow">→</div>
    <div class="step">
      <div class="label">Step 3</div><div class="name">Division Recommend</div>
      <div class="chip-row"><span class="chip">Mobile</span><span class="chip">Ecommerce</span><span class="chip">Fintech</span></div>
    </div>
    <div class="arrow">→</div>
    <div class="step primary"><div class="label">Step 4</div><div class="name">Notification</div></div>
  </div>
</div>
""");

    if (run is null)
    {
      html.Append("<div class=\"card\"><div class=\"empty\">No workflow has run yet.</div></div></div>");
      return html.ToString();
    }

    html.Append($"""
<div class="grid">
  <div class="card">
    <h2>Latest execution</h2>
    <div class="metric-grid">
      <div class="metric"><div class="k">Run ID</div><div class="v" style="font-size:13px">{System.Net.WebUtility.HtmlEncode(run.RunId.ToString())}</div><div class="s">execution identifier</div></div>
      <div class="metric"><div class="k">Started</div><div class="v" style="font-size:13px">{System.Net.WebUtility.HtmlEncode(run.StartedAt.ToString("u"))}</div><div class="s">utc</div></div>
      <div class="metric"><div class="k">Completed</div><div class="v" style="font-size:13px">{System.Net.WebUtility.HtmlEncode(run.CompletedAt?.ToString("u") ?? "-")}</div><div class="s">utc</div></div>
      <div class="metric"><div class="k">Steps</div><div class="v">{run.Steps.Count}</div><div class="s">workflow steps</div></div>
    </div>
    <div style="margin-top:14px" class="muted"><strong>News:</strong> {System.Net.WebUtility.HtmlEncode(run.OriginalNewsText)}</div>
  </div>
""");

    html.Append("""
  <div class="card">
    <h2>Context summary</h2>
    <div class="metric-grid">
""");
    if (context is not null)
    {
      html.Append($"      <div class=\"metric\"><div class=\"k\">Search hints</div><div class=\"v\" style=\"font-size:14px\">{System.Net.WebUtility.HtmlEncode(string.Join(", ", context.SearchHints))}</div></div>\n");
      html.Append($"      <div class=\"metric\"><div class=\"k\">Impact divisions</div><div class=\"v\" style=\"font-size:14px\">{System.Net.WebUtility.HtmlEncode(string.Join(", ", context.ImpactResult?.ImpactScores.Select(s => s.Division.ToString()) ?? []))}</div></div>\n");
      html.Append($"      <div class=\"metric\"><div class=\"k\">Recommendations</div><div class=\"v\">{context.Recommendations.Count}</div></div>\n");
      html.Append($"      <div class=\"metric\"><div class=\"k\">Notification</div><div class=\"v\" style=\"font-size:14px\">{System.Net.WebUtility.HtmlEncode(string.Join(", ", context.NotificationResult?.Channels ?? []))}</div></div>\n");
    }
    else
    {
      html.Append("<div class=\"metric\"><div class=\"k\">Context</div><div class=\"v\" style=\"font-size:14px\">No latest context</div></div>");
    }
    html.Append("""
    </div>
  </div>
</div>
""");

    html.Append("""
<div class="card" style="margin-top:18px">
  <h2>Step timeline</h2>
  <table>
    <thead>
      <tr>
        <th>Step</th>
        <th>Status</th>
        <th align="right">Duration (ms)</th>
        <th>Started</th>
        <th>Completed</th>
        <th>Error</th>
      </tr>
    </thead>
    <tbody>
""");

    foreach (var step in run.Steps.OrderBy(s => s.StartedAt))
    {
      html.Append("<tr>");
      html.Append($"<td>{System.Net.WebUtility.HtmlEncode(step.StepName)}</td>");
      html.Append($"<td>{(step.Succeeded ? "<span class=\"status-ok\">Success</span>" : "<span class=\"status-ng\">Failed</span>")}</td>");
      html.Append($"<td align=\"right\">{step.DurationMilliseconds:N0}</td>");
      html.Append($"<td>{System.Net.WebUtility.HtmlEncode(step.StartedAt.ToString("u"))}</td>");
      html.Append($"<td>{System.Net.WebUtility.HtmlEncode(step.CompletedAt.ToString("u"))}</td>");
      html.Append($"<td class=\"muted\">{System.Net.WebUtility.HtmlEncode(step.ErrorMessage ?? string.Empty)}</td>");
      html.Append("</tr>");
    }

    html.Append("""
    </tbody>
  </table>
</div>
</div>
""");

    return html.ToString();
  }
}
