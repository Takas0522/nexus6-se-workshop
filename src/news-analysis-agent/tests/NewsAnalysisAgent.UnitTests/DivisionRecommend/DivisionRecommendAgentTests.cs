using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using NewsAnalysisAgent.Agents.DivisionRecommend;
using NewsAnalysisAgent.Agents.Infrastructure;
using NewsAnalysisAgent.Agents.Infrastructure.Knowledge;
using NewsAnalysisAgent.Models;
using NewsAnalysisAgent.Tools;

namespace NewsAnalysisAgent.UnitTests.DivisionRecommend;

public sealed class DivisionRecommendAgentTests
{
    [Fact]
    public async Task ThreeDivisionAgents_AddThreeRecommendations_WhenRunInParallel()
    {
        var context = CreateContext();
        var mobile = new CountingMobilePlugin(TimeSpan.FromMilliseconds(100));
        var ecommerce = new CountingEcommercePlugin(TimeSpan.FromMilliseconds(100));
        var fintech = new CountingFintechPlugin(TimeSpan.FromMilliseconds(100));
        var foundry = new RecommendationFoundryClient();
        var knowledge = new StaticKnowledgeProvider();
        var agents = new[]
        {
            new DivisionRecommendAgent(DivisionKind.Mobile, mobile, foundry, knowledge, NullLogger<DivisionRecommendAgent>.Instance),
            new DivisionRecommendAgent(DivisionKind.Ecommerce, ecommerce, foundry, knowledge, NullLogger<DivisionRecommendAgent>.Instance),
            new DivisionRecommendAgent(DivisionKind.Fintech, fintech, foundry, knowledge, NullLogger<DivisionRecommendAgent>.Instance)
        };

        await Task.WhenAll(agents.Select(agent => agent.RunAsync(context, CancellationToken.None)));

        Assert.Equal(3, context.Recommendations.Count);
        Assert.Contains(context.Recommendations, rec => rec.Division == DivisionKind.Mobile);
        Assert.Contains(context.Recommendations, rec => rec.Division == DivisionKind.Ecommerce);
        Assert.Contains(context.Recommendations, rec => rec.Division == DivisionKind.Fintech);
        Assert.Equal(1, mobile.RepresentativeCalls);
        Assert.Equal(1, ecommerce.RepresentativeCalls);
        Assert.Equal(1, fintech.RepresentativeCalls);
        Assert.Equal(3, foundry.Calls);
        Assert.Equal(3, knowledge.Calls);
    }

    [Fact]
    public async Task RunAsync_UsesThreadSafeRecommendationAdd_WhenRepeatedInParallel()
    {
        for (var i = 0; i < 20; i++)
        {
            var context = CreateContext();
            var foundry = new RecommendationFoundryClient();
            var knowledge = new StaticKnowledgeProvider();
            var agents = new[]
            {
                new DivisionRecommendAgent(DivisionKind.Mobile, new CountingMobilePlugin(), foundry, knowledge, NullLogger<DivisionRecommendAgent>.Instance),
                new DivisionRecommendAgent(DivisionKind.Ecommerce, new CountingEcommercePlugin(), foundry, knowledge, NullLogger<DivisionRecommendAgent>.Instance),
                new DivisionRecommendAgent(DivisionKind.Fintech, new CountingFintechPlugin(), foundry, knowledge, NullLogger<DivisionRecommendAgent>.Instance)
            };

            await Task.WhenAll(agents.Select(agent => agent.RunAsync(context, CancellationToken.None)));

            Assert.Equal(3, context.Recommendations.Count);
            Assert.Equal(3, context.Recommendations.Select(rec => rec.Division).Distinct().Count());
        }
    }

    [Fact]
    public async Task RunAsync_FallsBack_WhenFoundryOutputIsMalformed()
    {
        var context = CreateContext();
        var agent = new DivisionRecommendAgent(
            DivisionKind.Mobile,
            new CountingMobilePlugin(),
            new MalformedFoundryClient(),
            new StaticKnowledgeProvider(),
            NullLogger<DivisionRecommendAgent>.Instance);

        await agent.RunAsync(context, CancellationToken.None);

        var recommendation = Assert.Single(context.Recommendations);
        Assert.Equal(DivisionKind.Mobile, recommendation.Division);
        Assert.Equal("(LLM parse failed)", recommendation.Headline);
        Assert.Equal(["KPI を再確認"], recommendation.NextActions);
    }

    [Fact]
    public async Task Factory_CreatesDivisionSpecificAgents_WithInjectedPlugins()
    {
        var context = CreateContext();
        var mobile = new CountingMobilePlugin();
        var ecommerce = new CountingEcommercePlugin();
        var fintech = new CountingFintechPlugin();
        var factory = new DivisionRecommendAgentFactory(
            mobile,
            ecommerce,
            fintech,
            new RecommendationFoundryClient(),
            new StaticKnowledgeProvider(),
            NullLogger<DivisionRecommendAgent>.Instance);

        await Task.WhenAll(
            factory.Create(DivisionKind.Mobile).RunAsync(context, CancellationToken.None),
            factory.Create(DivisionKind.Ecommerce).RunAsync(context, CancellationToken.None),
            factory.Create(DivisionKind.Fintech).RunAsync(context, CancellationToken.None));

        Assert.Equal(3, context.Recommendations.Count);
        Assert.Equal(1, mobile.DetailedCalls);
        Assert.Equal(1, ecommerce.DetailedCalls);
        Assert.Equal(1, fintech.DetailedCalls);
    }

    private static NewsAnalysisContext CreateContext() => new()
    {
        OriginalNewsText = "円安と競合施策のニュース",
        WebResearchResult = new WebResearchResult("為替と競合影響", ["fx", "competition"], ["https://example.com"]),
        ImpactResult = new BusinessImpactResult(
            [
                new ImpactScore(DivisionKind.Mobile, 4.0, "high"),
                new ImpactScore(DivisionKind.Ecommerce, 3.0, "medium"),
                new ImpactScore(DivisionKind.Fintech, 4.5, "high")
            ],
            ["mobile reason", "ec reason", "fintech reason"],
            [DivisionKind.Fintech, DivisionKind.Mobile, DivisionKind.Ecommerce])
    };

    private sealed class RecommendationFoundryClient : IFoundryAgentClient
    {
        private int _calls;
        public int Calls => _calls;

        public Task<string> InvokeAsync(string instructions, string userMessage, IReadOnlyList<object> tools, CancellationToken ct = default)
        {
            Interlocked.Increment(ref _calls);
            using var document = JsonDocument.Parse(userMessage);
            var division = document.RootElement.GetProperty("target_division").GetString() ?? "unknown";
            return Task.FromResult(JsonSerializer.Serialize(new
            {
                division,
                headline = $"{division} headline",
                next_actions = new[] { $"{division} action" },
                data_references = new[] { $"{division}_ai.risk_summary" }
            }));
        }
    }

    private sealed class MalformedFoundryClient : IFoundryAgentClient
    {
        public Task<string> InvokeAsync(string instructions, string userMessage, IReadOnlyList<object> tools, CancellationToken ct = default) =>
            Task.FromResult("not-json");
    }

    private sealed class StaticKnowledgeProvider : IKnowledgeProvider
    {
        private int _calls;
        public int Calls => _calls;

        public Task<IReadOnlyList<KnowledgeSnippet>> SearchAsync(
            string query,
            int maxResults = 6,
            int maxSnippetLength = 1000,
            CancellationToken ct = default)
        {
            Interlocked.Increment(ref _calls);
            return Task.FromResult<IReadOnlyList<KnowledgeSnippet>>(
                [new KnowledgeSnippet("mobile", "skill", "knowledge")]);
        }
    }

    private sealed class CountingMobilePlugin(TimeSpan? delay = null) : IMobileDataPlugin
    {
        private int _representativeCalls;
        private int _detailedCalls;
        public int RepresentativeCalls => _representativeCalls;
        public int DetailedCalls => _detailedCalls;

        public async Task<string> GetRepresentativeKpisAsync(string scenario, CancellationToken ct = default)
        {
            Interlocked.Increment(ref _representativeCalls);
            if (delay is not null) await Task.Delay(delay.Value, ct);
            return "{\"source\":\"mobile-test\"}";
        }

        public async Task<string> GetDetailedKpisAsync(string scenario, CancellationToken ct = default)
        {
            Interlocked.Increment(ref _detailedCalls);
            if (delay is not null) await Task.Delay(delay.Value, ct);
            return "{\"detail\":\"mobile-test\"}";
        }
    }

    private sealed class CountingEcommercePlugin(TimeSpan? delay = null) : IEcommerceDataPlugin
    {
        private int _representativeCalls;
        private int _detailedCalls;
        public int RepresentativeCalls => _representativeCalls;
        public int DetailedCalls => _detailedCalls;

        public async Task<string> GetRepresentativeKpisAsync(string scenario, CancellationToken ct = default)
        {
            Interlocked.Increment(ref _representativeCalls);
            if (delay is not null) await Task.Delay(delay.Value, ct);
            return "{\"source\":\"ecommerce-test\"}";
        }

        public async Task<string> GetDetailedKpisAsync(string scenario, CancellationToken ct = default)
        {
            Interlocked.Increment(ref _detailedCalls);
            if (delay is not null) await Task.Delay(delay.Value, ct);
            return "{\"detail\":\"ecommerce-test\"}";
        }
    }

    private sealed class CountingFintechPlugin(TimeSpan? delay = null) : IFintechDataPlugin
    {
        private int _representativeCalls;
        private int _detailedCalls;
        public int RepresentativeCalls => _representativeCalls;
        public int DetailedCalls => _detailedCalls;

        public async Task<string> GetRepresentativeKpisAsync(string scenario, CancellationToken ct = default)
        {
            Interlocked.Increment(ref _representativeCalls);
            if (delay is not null) await Task.Delay(delay.Value, ct);
            return "{\"source\":\"fintech-test\"}";
        }

        public async Task<string> GetDetailedKpisAsync(string scenario, CancellationToken ct = default)
        {
            Interlocked.Increment(ref _detailedCalls);
            if (delay is not null) await Task.Delay(delay.Value, ct);
            return "{\"detail\":\"fintech-test\"}";
        }
    }
}
