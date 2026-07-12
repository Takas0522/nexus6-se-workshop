using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NewsAnalysisAgent.Agents.BusinessImpact;
using NewsAnalysisAgent.Agents.Infrastructure;
using NewsAnalysisAgent.Agents.Infrastructure.Knowledge;
using NewsAnalysisAgent.Models;
using NewsAnalysisAgent.Tools;

namespace NewsAnalysisAgent.UnitTests.BusinessImpact;

public sealed class BusinessImpactAgentTests
{
    [Fact]
    public async Task RunAsync_ParsesImpactScoresForThreeDivisions()
    {
        var agent = CreateAgent(new StaticFoundryAgentClient("""
        {
          "impact_scores": [
            { "division": "mobile", "score": 3.8, "risk_level": "high" },
            { "division": "ecommerce", "score": 2.4, "risk_level": "medium" },
            { "division": "fintech", "score": 4.6, "risk_level": "high" }
          ],
          "impact_reasons": ["mobile reason", "ecommerce reason", "fintech reason"],
          "priority_order": ["fintech", "mobile", "ecommerce"]
        }
        """));
        var context = new NewsAnalysisContext
        {
            OriginalNewsText = "円安で海外調達と金融商品に影響",
            WebResearchResult = new WebResearchResult("円安が継続", ["為替", "調達"], ["https://example.com"])
        };

        await agent.RunAsync(context, CancellationToken.None);

        Assert.NotNull(context.ImpactResult);
        Assert.Equal(3, context.ImpactResult!.ImpactScores.Length);
        Assert.Contains(context.ImpactResult.ImpactScores, score => score.Division == "mobile");
        Assert.Contains(context.ImpactResult.ImpactScores, score => score.Division == "ecommerce");
        Assert.Contains(context.ImpactResult.ImpactScores, score => score.Division == "fintech");
    }

    [Fact]
    public async Task RunAsync_RebuildsPriorityOrderFromScoresDescending()
    {
        var agent = CreateAgent(new StaticFoundryAgentClient("""
        {
          "impact_scores": [
            { "division": "mobile", "score": 1.0, "risk_level": "low" },
            { "division": "ecommerce", "score": 5.0, "risk_level": "high" },
            { "division": "fintech", "score": 3.0, "risk_level": "medium" }
          ],
          "impact_reasons": ["mobile reason", "ecommerce reason", "fintech reason"],
          "priority_order": ["mobile", "fintech", "ecommerce"]
        }
        """));

        var context = new NewsAnalysisContext { OriginalNewsText = "ポイント競争" };
        await agent.RunAsync(context, CancellationToken.None);

        Assert.Equal(["ecommerce", "fintech", "mobile"], context.ImpactResult!.PriorityOrder);
        Assert.Equal([5.0, 3.0, 1.0], context.ImpactResult.ImpactScores.Select(score => score.Score).ToArray());
    }

    [Fact]
    public async Task RunAsync_FallsBackToKpiHeuristicWhenLlmOutputIsMalformed()
    {
        var agent = CreateAgent(new StaticFoundryAgentClient("not-json"));
        var context = new NewsAnalysisContext { OriginalNewsText = "市場変動" };

        await agent.RunAsync(context, CancellationToken.None);

        Assert.NotNull(context.ImpactResult);
        Assert.Equal(3, context.ImpactResult!.ImpactScores.Length);
        Assert.Equal(
            context.ImpactResult.ImpactScores.OrderByDescending(score => score.Score).Select(score => score.Division),
            context.ImpactResult.PriorityOrder);
        Assert.All(context.ImpactResult.ImpactReasons, reason => Assert.Contains("heuristic", reason, StringComparison.OrdinalIgnoreCase));
    }

    private static BusinessImpactAgent CreateAgent(IFoundryAgentClient foundryAgentClient) => new(
        foundryAgentClient,
        new MockFabricDataPlugin(),
        new StaticKnowledgeProvider(),
        Options.Create(new DivisionsConfig
        {
            Divisions = [
                new DivisionConfig { Id = "mobile", Label = "Mobile" },
                new DivisionConfig { Id = "ecommerce", Label = "Ecommerce" },
                new DivisionConfig { Id = "fintech", Label = "Fintech" }
            ]
        }),
        NullLogger<BusinessImpactAgent>.Instance);

    private sealed class StaticFoundryAgentClient(string response) : IFoundryAgentClient
    {
        public Task<string> InvokeAsync(string instructions, string userMessage, IReadOnlyList<object> tools, CancellationToken ct = default) =>
            Task.FromResult(response);
    }

    private sealed class StaticKnowledgeProvider : IKnowledgeProvider
    {
        public Task<IReadOnlyList<KnowledgeSnippet>> SearchAsync(string query, int maxResults = 6, int maxSnippetLength = 1000, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<KnowledgeSnippet>>([
                new KnowledgeSnippet("mobile", "mobile_skill_fx-impact", "為替影響時は端末コストとMNPを確認する。")
            ]);
    }
}
