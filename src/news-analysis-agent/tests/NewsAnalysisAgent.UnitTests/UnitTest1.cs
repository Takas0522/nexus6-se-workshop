using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NewsAnalysisAgent.Agents;
using NewsAnalysisAgent.Agents.BusinessImpact;
using NewsAnalysisAgent.Agents.DivisionRecommend;
using NewsAnalysisAgent.Agents.Notification;
using NewsAnalysisAgent.Agents.WebResearch;
using NewsAnalysisAgent.Agents.Infrastructure;
using NewsAnalysisAgent.Agents.Infrastructure.Knowledge;
using NewsAnalysisAgent.Host.Queue;
using NewsAnalysisAgent.Tools;
using NewsAnalysisAgent.Models;
using NewsAnalysisAgent.Orchestration;

namespace NewsAnalysisAgent.UnitTests;

public sealed class CoreWorkflowTests
{
    [Fact]
    public void NewsAnalysisContext_SerializesRoundTrip()
    {
        var context = new NewsAnalysisContext
        {
            OriginalNewsText = "円安ニュース",
            WebResearchResult = new WebResearchResult("summary", ["fx"], ["https://example.com"]),
            ImpactResult = new BusinessImpactResult(
                [new ImpactScore("Mobile", 4.5, "high")],
                ["reason"],
                ["Mobile"]),
            Recommendations = [new DivisionRecommendation("Mobile", "headline", [new NextAction("act", "")], ["data"])],
            NotificationResult = new NotificationResult(true, ["mock"], "{}")
        };

        var json = JsonSerializer.Serialize(context);
        var actual = JsonSerializer.Deserialize<NewsAnalysisContext>(json);

        Assert.NotNull(actual);
        Assert.Equal(context.OriginalNewsText, actual!.OriginalNewsText);
        Assert.Equal("summary", actual.WebResearchResult!.Summary);
        Assert.Equal("Mobile", actual.ImpactResult!.ImpactScores[0].Division);
        Assert.Single(actual.Recommendations);
        Assert.True(actual.NotificationResult!.Sent);
    }

    [Fact]
    public async Task Workflow_RunsAllStubSteps()
    {
        var store = new WorkflowExecutionStore();
        var builder = new NewsAnalysisWorkflowBuilder(
            new WebResearchAgent(
                new MockFoundryAgentClient(),
                new MockBingSearchPlugin(),
                new EmptyWebPageFetchPlugin(),
                NullLogger<WebResearchAgent>.Instance),
            new BusinessImpactAgent(
                new MockFoundryAgentClient(),
                new MockFabricDataPlugin(),
                new LocalFolderKnowledgeProvider(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", "knowledge"), NullLogger<LocalFolderKnowledgeProvider>.Instance),
                NullLogger<BusinessImpactAgent>.Instance),
            new DivisionRecommendAgentFactory(
                new MockMobileDataPlugin(),
                new MockEcommerceDataPlugin(),
                new MockFintechDataPlugin(),
                new MockFoundryAgentClient(),
                new StaticKnowledgeProvider(),
                NullLogger<DivisionRecommendAgent>.Instance),
            new NotificationAgent(new TestTeamsPlugin(), NullLogger<NotificationAgent>.Instance),
            store,
            NullLogger<NewsAnalysisWorkflow>.Instance);

        var workflow = builder.Build();
        var result = await workflow.RunAsync(new NewsAnalysisContext { OriginalNewsText = "テスト" });

        Assert.NotNull(result.WebResearchResult);
        Assert.NotNull(result.ImpactResult);
        Assert.Equal(3, result.Recommendations.Count);
        Assert.NotNull(result.NotificationResult);
        Assert.Equal(6, store.LatestRun!.Steps.Count);
        Assert.All(store.LatestRun.Steps, step => Assert.True(step.Succeeded));
    }

    [Fact]
    public async Task Workflow_RunsDivisionFanOutInParallel()
    {
        var tracker = new ConcurrencyTracker();
        var divisionSteps = new[]
        {
            ("mobile", (IWorkflowStep<NewsAnalysisContext>)new DelayStep(TimeSpan.FromMilliseconds(250), tracker.Enter)),
            ("ecommerce", (IWorkflowStep<NewsAnalysisContext>)new DelayStep(TimeSpan.FromMilliseconds(250), tracker.Enter)),
            ("fintech", (IWorkflowStep<NewsAnalysisContext>)new DelayStep(TimeSpan.FromMilliseconds(250), tracker.Enter))
        };

        var workflow = new NewsAnalysisWorkflow(
            new NoopStep(),
            new NoopStep(),
            divisionSteps,
            new NoopStep(),
            new WorkflowExecutionStore(),
            NullLogger<NewsAnalysisWorkflow>.Instance);

        var stopwatch = Stopwatch.StartNew();
        await workflow.RunAsync(new NewsAnalysisContext { OriginalNewsText = "parallel" });
        stopwatch.Stop();

        Assert.Equal(3, tracker.MaxActive);
        Assert.True(stopwatch.ElapsedMilliseconds < 900, $"Expected parallel fan-out, elapsed {stopwatch.ElapsedMilliseconds} ms.");
    }

    [Fact]
    public async Task QueueBackgroundService_DequeuesRunsWorkflowAndDeletesMessage()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Storage:Account"] = "localtest" })
            .Build();
        var queueClient = new FakeQueueClient("m1", "r1", "{\"originalNewsText\":\"キューテスト\"}");
        var workflow = new CapturingWorkflow();
        var service = new QueueBackgroundService(
            configuration,
            new FakeQueueClientFactory(queueClient),
            workflow,
            NullLogger<QueueBackgroundService>.Instance);

        var processed = await service.ExecuteOnePollAsync(queueClient, CancellationToken.None);

        Assert.True(processed);
        Assert.Equal("キューテスト", workflow.Context!.OriginalNewsText);
        Assert.True(queueClient.Deleted);
    }

    private sealed class EmptyWebPageFetchPlugin : IWebPageFetchPlugin
    {
        public Task<string> FetchAsync(string url, CancellationToken ct = default) => Task.FromResult(string.Empty);
    }

    private sealed class TestWebPageFetchPlugin : IWebPageFetchPlugin
    {
        public Task<string> FetchAsync(string url, CancellationToken ct = default) => Task.FromResult("test page");
    }

    private sealed class TestTeamsPlugin : ITeamsNotificationPlugin
    {
        public Task<string> SendAsync(string division, string cardPayloadJson, CancellationToken ct = default) =>
            Task.FromResult($"test:{division}");
    }

    private sealed class StaticKnowledgeProvider : IKnowledgeProvider
    {
        public Task<IReadOnlyList<KnowledgeSnippet>> SearchAsync(
            string query,
            int maxResults = 6,
            int maxSnippetLength = 1000,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<KnowledgeSnippet>>(
                [new KnowledgeSnippet("common", "test", "test knowledge")]);
    }

    private sealed class ConcurrencyTracker
    {
        private int _active;
        private int _maxActive;

        public int MaxActive => _maxActive;

        public IDisposable Enter()
        {
            var current = Interlocked.Increment(ref _active);
            int observed;
            do
            {
                observed = _maxActive;
                if (current <= observed)
                {
                    break;
                }
            } while (Interlocked.CompareExchange(ref _maxActive, current, observed) != observed);

            return new Release(() => Interlocked.Decrement(ref _active));
        }
    }

    private sealed class NoopStep : IWorkflowStep<NewsAnalysisContext>
    {
        public Task RunAsync(NewsAnalysisContext ctx, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class DelayStep(TimeSpan delay, Func<IDisposable> enter) : IWorkflowStep<NewsAnalysisContext>
    {
        public async Task RunAsync(NewsAnalysisContext ctx, CancellationToken ct)
        {
            using var _ = enter();
            await Task.Delay(delay, ct);
        }
    }

    private sealed class Release(Action release) : IDisposable
    {
        public void Dispose() => release();
    }

    private sealed class CapturingWorkflow : IWorkflow<NewsAnalysisContext>
    {
        public NewsAnalysisContext? Context { get; private set; }

        public Task<NewsAnalysisContext> RunAsync(NewsAnalysisContext context, CancellationToken ct = default)
        {
            Context = context;
            return Task.FromResult(context);
        }
    }

    private sealed class FakeQueueClientFactory(IQueueClient queueClient) : IQueueClientFactory
    {
        public IQueueClient CreateClient(string queueName) => queueClient;
    }

    private sealed class FakeQueueClient(string messageId, string popReceipt, string messageText) : IQueueClient
    {
        public bool Deleted { get; private set; }

        public Task CreateIfNotExistsAsync(CancellationToken ct) => Task.CompletedTask;

        public Task<QueueJobMessage?> ReceiveMessageAsync(TimeSpan visibilityTimeout, CancellationToken ct) =>
            Task.FromResult<QueueJobMessage?>(new QueueJobMessage(messageId, popReceipt, messageText));

        public Task DeleteMessageAsync(string id, string receipt, CancellationToken ct)
        {
            Assert.Equal(messageId, id);
            Assert.Equal(popReceipt, receipt);
            Deleted = true;
            return Task.CompletedTask;
        }
    }
}
