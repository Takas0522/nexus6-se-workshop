using System.Text;
using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Resilience;
using NewsAnalysisAgent.Agents.BusinessImpact;
using NewsAnalysisAgent.Agents.DivisionRecommend;
using NewsAnalysisAgent.Agents.Notification;
using NewsAnalysisAgent.Agents.WebResearch;
using NewsAnalysisAgent.Agents.Infrastructure;
using NewsAnalysisAgent.Agents.Infrastructure.Knowledge;
using NewsAnalysisAgent.Models;
using NewsAnalysisAgent.Orchestration;
using NewsAnalysisAgent.Tools;
using NewsAnalysisWebApp.Api.Models;
using Polly;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(builder.Configuration["Urls"] ?? "http://localhost:5100");

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

builder.Services.Configure<DivisionsConfig>(options =>
{
    var divisionsSection = builder.Configuration.GetSection("Divisions");
    if (divisionsSection.Exists())
    {
        var divisions = divisionsSection.Get<List<DivisionConfig>>() ?? [];
        options.Divisions = divisions;
    }
});

// CORS
var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? ["http://localhost:5173"];
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// Credentials
builder.Services.AddSingleton<TokenCredential>(_ => new DefaultAzureCredential());
builder.Services.AddHttpClient("foundry-agent", client => client.Timeout = TimeSpan.FromSeconds(120));
builder.Services.AddHttpClient("bing-grounding", client => client.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddHttpClient("webiq", client => client.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddHttpClient<WebPageFetchPlugin>(client => client.Timeout = TimeSpan.FromSeconds(10))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 3
    });

// Foundry clients
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

// Key Vault
var webappKeyVaultUri = builder.Configuration["KeyVault:Uri"];
if (!string.IsNullOrWhiteSpace(webappKeyVaultUri))
{
    builder.Services.AddSingleton(sp => new SecretClient(new Uri(webappKeyVaultUri), sp.GetRequiredService<TokenCredential>()));
}

// Plugins
builder.Services.AddSingleton<MockBingSearchPlugin>();
builder.Services.AddSingleton<MockFabricDataPlugin>();

// WebIQ or legacy Bing+WebPageFetch
var webappWebIqBaseUrl = builder.Configuration["WebIq:BaseUrl"];
if (!string.IsNullOrWhiteSpace(webappWebIqBaseUrl))
{
    builder.Services.AddSingleton<WebPageFetchPlugin>();
    builder.Services.AddSingleton(sp =>
    {
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
                  .LogWarning(ex, "Failed to retrieve WebIQ API key from Key Vault.");
                apiKey = "";
            }
        }
        else
        {
            apiKey = builder.Configuration["WebIq:ApiKey"] ?? "";
        }

        return new WebIqPlugin(
            webappWebIqBaseUrl,
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

// Agents
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

var app = builder.Build();
app.UseCors();

// Serve React SPA from wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();

// Serve news-portal static files under /news-portal/
var newsPortalPath = Path.Combine(app.Environment.ContentRootPath, "news-portal");
if (Directory.Exists(newsPortalPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(newsPortalPath),
        RequestPath = "/news-portal"
    });
}

// Health check
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

// News articles list
app.MapGet("/api/news", () =>
{
    var articles = new List<NewsArticle>
    {
        new("fx1", "急激な円安進行 — 1ドル160円突破、企業業績への影響拡大", "為替",
            "/news-portal/article-fx1.html",
            "為替市場で急激な円安が進行し、1ドル160円を突破。輸出企業には追い風だが、輸入コスト増加が懸念される。"),
        new("comp1", "大手通信3社の競合再編 — 業界地図が大きく変わる", "競合統合",
            "/news-portal/article-comp1.html",
            "通信業界で大型再編が加速。MNP競争の激化や料金プランの見直しが各社に迫られている。"),
        new("boj1", "日銀、追加利上げを決定 — 政策金利0.75%に", "日銀利上げ",
            "/news-portal/article-boj1.html",
            "日本銀行が政策金利を0.75%に引き上げ。住宅ローン金利や企業の資金調達コストへの影響が注目される。")
    };
    return Results.Ok(articles);
});

// Run analysis (async - returns immediately with job ID)
app.MapPost("/api/analysis/run", (
    AnalysisRequest request,
    IWorkflow<NewsAnalysisContext> workflow,
    WorkflowExecutionStore store) =>
{
    if (string.IsNullOrWhiteSpace(request.NewsText))
    {
        return Results.BadRequest(new { error = "newsText is required." });
    }

    var jobId = store.CreateJob();

    _ = Task.Run(async () =>
    {
        try
        {
            var context = new NewsAnalysisContext
            {
                OriginalNewsText = request.NewsText,
                SearchHints = request.SearchHints ?? []
            };
            await workflow.RunAsync(context, CancellationToken.None);
            store.CompleteJob(jobId, context);
        }
        catch (Exception ex)
        {
            store.FailJob(jobId, ex.Message);
        }
    });

    return Results.Accepted($"/api/analysis/status/{jobId}", new { runId = jobId });
});

// Poll analysis status
app.MapGet("/api/analysis/status/{runId}", (string runId, WorkflowExecutionStore store) =>
{
    var job = store.GetJob(runId);
    if (job is null)
    {
        return Results.NotFound(new { error = "Job not found." });
    }

    if (job.Status == "running")
    {
        return Results.Ok(new { status = "running" });
    }

    if (job.Status == "failed")
    {
        return Results.Ok(new { status = "failed", error = job.Error });
    }

    var ctx = job.Context!;
    var response = new AnalysisResponse(
        ctx.WebResearchResult,
        ctx.ImpactResult,
        ctx.Recommendations,
        ctx.StartedAt,
        ctx.CompletedAt);
    return Results.Ok(new { status = "completed", result = response });
});

// Latest result
app.MapGet("/api/analysis/latest", (WorkflowExecutionStore store) =>
{
    var context = store.LatestContext;
    if (context is null)
    {
        return Results.NotFound(new { error = "No analysis has been run yet." });
    }

    var response = new AnalysisResponse(
        context.WebResearchResult,
        context.ImpactResult,
        context.Recommendations,
        context.StartedAt,
        context.CompletedAt);

    return Results.Ok(response);
});

// SPA fallback: non-API routes serve index.html
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
