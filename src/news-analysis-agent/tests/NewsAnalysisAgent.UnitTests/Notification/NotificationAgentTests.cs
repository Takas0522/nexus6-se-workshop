using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NewsAnalysisAgent.Agents.Notification;
using NewsAnalysisAgent.Models;
using NewsAnalysisAgent.Tools;
using Polly;

namespace NewsAnalysisAgent.UnitTests.Notification;

public sealed class NotificationAgentTests
{
    [Fact]
    public async Task NotificationAgent_GeneratesThreeAdaptiveCardsAndSendsAllDivisions()
    {
        var plugin = new CapturingTeamsPlugin();
        var agent = new NotificationAgent(plugin, NullLogger<NotificationAgent>.Instance);
        var context = CreateContext();

        await agent.RunAsync(context, CancellationToken.None);

        Assert.NotNull(context.NotificationResult);
        Assert.True(context.NotificationResult!.Sent);
        Assert.Equal(["captured:Ecommerce", "captured:Fintech", "captured:Mobile"], context.NotificationResult.Channels);
        Assert.Equal(["Ecommerce", "Fintech", "Mobile"], plugin.Calls.Select(call => call.Division).ToArray());
        Assert.All(plugin.Calls, call => AssertAdaptiveCardShape(call.CardPayloadJson));
    }

    [Fact]
    public async Task TeamsWorkflowsPlugin_UsesMockOnlyForDivisionWithoutConfiguredUrl()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Accepted));
        var plugin = CreateTeamsWorkflowsPlugin(handler, new Dictionary<string, string?>
        {
            ["Teams:WorkflowsUrl:Mobile"] = "https://workflows.local/mobile",
            ["Teams:WorkflowsUrl:Ecommerce"] = "",
            ["Teams:WorkflowsUrl:Fintech"] = "https://workflows.local/fintech"
        });

        var mobile = await plugin.SendAsync("Mobile", SampleCard(), CancellationToken.None);
        var ecommerce = await plugin.SendAsync("Ecommerce", SampleCard(), CancellationToken.None);
        var fintech = await plugin.SendAsync("Fintech", SampleCard(), CancellationToken.None);

        Assert.Equal("teams:Mobile", mobile);
        Assert.Equal("mock:Ecommerce", ecommerce);
        Assert.Equal("teams:Fintech", fintech);
        Assert.Equal(["https://workflows.local/mobile", "https://workflows.local/fintech"], handler.RequestUris.Select(uri => uri.ToString()).ToArray());
    }

    [Fact]
    public async Task NotificationAgent_ContinuesPartialSuccessWhenTeamsWorkflowReturnsHttp500AfterRetry()
    {
        var attemptsByDivision = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var handler = new RecordingHandler(request =>
        {
            var division = request.RequestUri!.Segments.Last().TrimEnd('/');
            attemptsByDivision[division] = attemptsByDivision.GetValueOrDefault(division) + 1;
            return new HttpResponseMessage(division.Equals("mobile", StringComparison.OrdinalIgnoreCase)
                ? HttpStatusCode.InternalServerError
                : HttpStatusCode.Accepted);
        });
        var plugin = CreateTeamsWorkflowsPlugin(handler, new Dictionary<string, string?>
        {
            ["Teams:WorkflowsUrl:Mobile"] = "https://workflows.local/mobile",
            ["Teams:WorkflowsUrl:Ecommerce"] = "https://workflows.local/ecommerce",
            ["Teams:WorkflowsUrl:Fintech"] = "https://workflows.local/fintech"
        });
        var agent = new NotificationAgent(plugin, NullLogger<NotificationAgent>.Instance);
        var context = CreateContext();

        await agent.RunAsync(context, CancellationToken.None);

        Assert.True(context.NotificationResult!.Sent);
        Assert.Equal(["teams:Ecommerce", "teams:Fintech"], context.NotificationResult.Channels);
        Assert.Equal(2, attemptsByDivision["mobile"]);
        Assert.Equal(1, attemptsByDivision["ecommerce"]);
        Assert.Equal(1, attemptsByDivision["fintech"]);
    }

    [Fact]
    public async Task GraphTeamsPlugin_PostsConfiguredDivisionToGraph()
    {
        var handler = new RecordingHandler(request => request.RequestUri!.Host.Equals("login.microsoftonline.com", StringComparison.OrdinalIgnoreCase)
            ? JsonResponse("""{"access_token":"delegated-token","refresh_token":"refresh-token"}""")
            : new HttpResponseMessage(HttpStatusCode.Created));
        var plugin = CreateGraphTeamsPlugin(handler, new Dictionary<string, string?>
        {
            ["Teams:Graph:Mobile:TeamId"] = "team-mobile",
            ["Teams:Graph:Mobile:ChannelId"] = "19:mobile@thread.tacv2",
            ["Teams:Graph:ClientId"] = "client-id",
            ["Teams:Graph:TenantId"] = "tenant-id",
            ["Teams:Graph:RefreshToken"] = "refresh-token"
        });

        var result = await plugin.SendAsync("Mobile", SampleCard(), CancellationToken.None);

        Assert.Equal("teams-graph:Mobile", result);
        Assert.Equal("https://graph.microsoft.com/v1.0/teams/team-mobile/channels/19%3Amobile%40thread.tacv2/messages", handler.RequestUris.Last().ToString());
        Assert.Equal("Bearer", handler.AuthorizationHeaders.Last()?.Scheme);
        Assert.Equal("delegated-token", handler.AuthorizationHeaders.Last()?.Parameter);
    }

    [Fact]
    public async Task GraphTeamsPlugin_FallsBackToMockWhenGraphFails()
    {
        var handler = new RecordingHandler(request => request.RequestUri!.Host.Equals("login.microsoftonline.com", StringComparison.OrdinalIgnoreCase)
            ? JsonResponse("""{"access_token":"delegated-token","refresh_token":"refresh-token"}""")
            : new HttpResponseMessage(HttpStatusCode.Forbidden));
        var plugin = CreateGraphTeamsPlugin(handler, new Dictionary<string, string?>
        {
            ["Teams:Graph:Fintech:TeamId"] = "team-fintech",
            ["Teams:Graph:Fintech:ChannelId"] = "19:fintech@thread.tacv2",
            ["Teams:Graph:ClientId"] = "client-id",
            ["Teams:Graph:TenantId"] = "tenant-id",
            ["Teams:Graph:RefreshToken"] = "refresh-token"
        });

        var result = await plugin.SendAsync("Fintech", SampleCard(), CancellationToken.None);

        Assert.Equal("mock:Fintech", result);
        Assert.Equal(2, handler.RequestUris.Count);
    }

    [Fact]
    public void AdaptiveCardTemplate_IncludesRequiredTeamsWorkflowEnvelopeAndCardElements()
    {
        var card = AdaptiveCardTemplates.Build(
            new DivisionRecommendation("Mobile", "headline loan_balance", [new NextAction("action loan_balance", "26325000.0000 JPY を確認")], ["mobile_skill_competitor-mnp.md"])
            {
                SourceFiles = ["mobile_skill_competitor-mnp.md"],
                KpiReferences = [new KpiReference("loan_balance", "", "26325", "JPY", "fintech_ai.loan_balances")]
            },
            "high");

        AssertAdaptiveCardShape(card);
        using var document = JsonDocument.Parse(card);
        var content = document.RootElement.GetProperty("attachments")[0].GetProperty("content");
        Assert.Contains("headline", content.GetRawText());
        Assert.DoesNotContain("loan_balance", content.GetRawText());
        Assert.Contains("action", content.GetRawText());
        var textValues = CollectTextValues(content);
        Assert.Contains(textValues, text => text.Contains("26,325 円", StringComparison.Ordinal));
        Assert.Contains(textValues, text => text.Contains("26,325,000 円", StringComparison.Ordinal));
        Assert.DoesNotContain("mobile_skill_competitor-mnp.md", content.GetRawText());
        Assert.Contains("Table", content.GetRawText());
        Assert.Contains("high", content.GetRawText());
    }

    [Fact]
    public void AdaptiveCardTemplate_FiltersMockWebReferencesAndFormatsPercentAndCounts()
    {
        var card = AdaptiveCardTemplates.Build(
            new DivisionRecommendation("Fintech", "headline", [new NextAction("利上げ確認", "平均貸出金利 0.0500 % と 2.0000 件を確認")], [])
            {
                CategorizedReferences = new CategorizedReferences(
                    [
                        new KpiReference("avg_interest_rate", "", "0.0500", "%", "fintech_ai.risk_summary"),
                        new KpiReference("negative_credit_reviews", "", "2.0000", "件", "fintech_ai.risk_summary")
                    ],
                    [],
                    [new WebReference("mock", "https://example.com/mock-news")],
                    [])
            },
            "high");

        using var document = JsonDocument.Parse(card);
        var raw = document.RootElement.GetRawText();
        var textValues = CollectTextValues(document.RootElement);
        Assert.Contains(textValues, text => text.Contains("5.00 %", StringComparison.Ordinal));
        Assert.Contains(textValues, text => text.Contains("2 件", StringComparison.Ordinal));
        Assert.DoesNotContain("example.com", raw);
        Assert.Contains("Container", raw);
    }

    private static NewsAnalysisContext CreateContext() => new()
    {
        OriginalNewsText = "テスト",
        ImpactResult = new BusinessImpactResult(
            [
                new ImpactScore("Mobile", 4.5, "high"),
                new ImpactScore("Ecommerce", 3.1, "medium"),
                new ImpactScore("Fintech", 4.8, "high")
            ],
            ["reason"],
            ["Mobile", "Fintech", "Ecommerce"]),
        Recommendations =
        [
            new DivisionRecommendation("Mobile", "Mobile headline", [new NextAction("Mobile action", "")], ["mobile-data"]),
            new DivisionRecommendation("Ecommerce", "Ecommerce headline", [new NextAction("Ecommerce action", "")], ["ecommerce-data"]),
            new DivisionRecommendation("Fintech", "Fintech headline", [new NextAction("Fintech action", "")], ["fintech-data"])
        ]
    };

    private static TeamsWorkflowsPlugin CreateTeamsWorkflowsPlugin(RecordingHandler handler, Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddResiliencePipeline("agent-retry", pipeline => pipeline.AddRetry(new Polly.Retry.RetryStrategyOptions
        {
            MaxRetryAttempts = 1,
            Delay = TimeSpan.Zero
        }));
        var provider = services.BuildServiceProvider().GetRequiredService<Polly.Registry.ResiliencePipelineProvider<string>>();

        return new TeamsWorkflowsPlugin(
            configuration,
            new StaticHttpClientFactory(new HttpClient(handler)),
            new MockTeamsPlugin(NullLogger<MockTeamsPlugin>.Instance),
            NullLogger<TeamsWorkflowsPlugin>.Instance,
            provider);
    }

    private static GraphTeamsPlugin CreateGraphTeamsPlugin(RecordingHandler handler, Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new GraphTeamsPlugin(
            configuration,
            new StaticHttpClientFactory(new HttpClient(handler)),
            new MockTeamsPlugin(NullLogger<MockTeamsPlugin>.Instance),
            NullLogger<GraphTeamsPlugin>.Instance);
    }

    private static string SampleCard() => AdaptiveCardTemplates.Build(
        new DivisionRecommendation("Mobile", "headline", [new NextAction("action", "")], ["mobile_skill_competitor-mnp.md"])
        {
            SourceFiles = ["mobile_skill_competitor-mnp.md"]
        },
        "high");

    private static void AssertAdaptiveCardShape(string payloadJson)
    {
        using var document = JsonDocument.Parse(payloadJson);
        Assert.Equal("message", document.RootElement.GetProperty("type").GetString());
        var attachment = document.RootElement.GetProperty("attachments")[0];
        Assert.Equal("application/vnd.microsoft.card.adaptive", attachment.GetProperty("contentType").GetString());
        var content = attachment.GetProperty("content");
        Assert.Equal("AdaptiveCard", content.GetProperty("type").GetString());
        Assert.Equal("1.5", content.GetProperty("version").GetString());
        Assert.True(content.GetProperty("body").GetArrayLength() > 0);
        Assert.True(content.GetProperty("actions").GetArrayLength() > 0);
    }

    private static IReadOnlyList<string> CollectTextValues(JsonElement element)
    {
        var values = new List<string>();
        Collect(element, values);
        return values;
    }

    private static void Collect(JsonElement element, List<string> values)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
            {
                values.Add(text.GetString() ?? string.Empty);
            }

            foreach (var property in element.EnumerateObject())
            {
                Collect(property.Value, values);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                Collect(item, values);
            }
        }
    }

    private sealed class CapturingTeamsPlugin : ITeamsNotificationPlugin
    {
        public List<(string Division, string CardPayloadJson)> Calls { get; } = [];

        public Task<string> SendAsync(string division, string cardPayloadJson, CancellationToken ct = default)
        {
            Calls.Add((division, cardPayloadJson));
            return Task.FromResult($"captured:{division}");
        }
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public List<Uri> RequestUris { get; } = [];
        public List<System.Net.Http.Headers.AuthenticationHeaderValue?> AuthorizationHeaders { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUris.Add(request.RequestUri!);
            AuthorizationHeaders.Add(request.Headers.Authorization);
            return Task.FromResult(responseFactory(request));
        }
    }

    private sealed class StaticHttpClientFactory(HttpClient httpClient) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => httpClient;
    }

    private static HttpResponseMessage JsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
    }
}
