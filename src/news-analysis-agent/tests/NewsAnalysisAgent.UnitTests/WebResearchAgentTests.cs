using NewsAnalysisAgent.Agents.Infrastructure;
using NewsAnalysisAgent.Agents.WebResearch;
using NewsAnalysisAgent.Models;
using NewsAnalysisAgent.Tools;
using Microsoft.Extensions.Logging.Abstractions;

namespace NewsAnalysisAgent.UnitTests;

public sealed class WebResearchAgentTests
{
    [Fact]
    public async Task RunAsync_ParsesMockFoundryJsonIntoWebResearchResult()
    {
        var agent = new WebResearchAgent(
            new MockFoundryAgentClient(),
            new StaticBingSearchPlugin(),
            new StaticWebPageFetchPlugin(),
            NullLogger<WebResearchAgent>.Instance);
        var context = new NewsAnalysisContext
        {
            OriginalNewsText = "円安が国内小売へ影響",
            SearchHints = ["為替", "小売"]
        };

        await agent.RunAsync(context, CancellationToken.None);

        Assert.NotNull(context.WebResearchResult);
        Assert.Equal("Mock web research summary for local execution.", context.WebResearchResult!.Summary);
        Assert.Equal(3, context.WebResearchResult.KeyFactors.Length);
        Assert.Equal(["https://example.com/mock-news", "https://example.com/mock-market"], context.WebResearchResult.SourceUrls);
    }

    [Fact]
    public async Task RunAsync_FallsBackWhenFoundryOutputIsMalformed()
    {
        var agent = new WebResearchAgent(
            new MalformedFoundryAgentClient(),
            new StaticBingSearchPlugin(),
            new StaticWebPageFetchPlugin(),
            NullLogger<WebResearchAgent>.Instance);
        var context = new NewsAnalysisContext { OriginalNewsText = "テスト" };

        await agent.RunAsync(context, CancellationToken.None);

        Assert.NotNull(context.WebResearchResult);
        Assert.StartsWith("(parse-failed)", context.WebResearchResult!.Summary);
        Assert.Empty(context.WebResearchResult.KeyFactors);
        Assert.Empty(context.WebResearchResult.SourceUrls);
    }

    private sealed class StaticBingSearchPlugin : IBingSearchPlugin
    {
        public Task<string> SearchAsync(string query, CancellationToken ct = default) => Task.FromResult("""
        {
          "results": [
            { "title": "sample", "url": "https://example.com/source" }
          ]
        }
        """);
    }

    private sealed class StaticWebPageFetchPlugin : IWebPageFetchPlugin
    {
        public Task<string> FetchAsync(string url, CancellationToken ct = default) => Task.FromResult("sample page body");
    }

    private sealed class MalformedFoundryAgentClient : IFoundryAgentClient
    {
        public Task<string> InvokeAsync(string instructions, string userMessage, IReadOnlyList<object> tools, CancellationToken ct = default) =>
            Task.FromResult("not-json");
    }
}
