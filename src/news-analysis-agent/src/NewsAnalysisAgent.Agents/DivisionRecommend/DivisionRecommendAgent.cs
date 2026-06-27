using System.Text.Json;
using Microsoft.Extensions.Logging;
using NewsAnalysisAgent.Agents.Infrastructure;
using NewsAnalysisAgent.Agents.Infrastructure.Knowledge;
using NewsAnalysisAgent.Models;
using NewsAnalysisAgent.Tools;

namespace NewsAnalysisAgent.Agents.DivisionRecommend;

public sealed class DivisionRecommendAgent : IWorkflowStep<NewsAnalysisContext>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IFoundryAgentClient _foundryAgentClient;
    private readonly IKnowledgeProvider _knowledgeProvider;
    private readonly object _dataPlugin;
    private readonly Func<string, CancellationToken, Task<string>> _representativeKpis;
    private readonly Func<string, CancellationToken, Task<string>> _detailedKpis;
    private readonly ILogger<DivisionRecommendAgent> _logger;

    public DivisionKind Division { get; }

    public DivisionRecommendAgent(
        DivisionKind division,
        IMobileDataPlugin dataPlugin,
        IFoundryAgentClient foundryAgentClient,
        IKnowledgeProvider knowledgeProvider,
        ILogger<DivisionRecommendAgent> logger)
        : this(division, dataPlugin, dataPlugin.GetRepresentativeKpisAsync, dataPlugin.GetDetailedKpisAsync, foundryAgentClient, knowledgeProvider, logger)
    {
    }

    public DivisionRecommendAgent(
        DivisionKind division,
        IEcommerceDataPlugin dataPlugin,
        IFoundryAgentClient foundryAgentClient,
        IKnowledgeProvider knowledgeProvider,
        ILogger<DivisionRecommendAgent> logger)
        : this(division, dataPlugin, dataPlugin.GetRepresentativeKpisAsync, dataPlugin.GetDetailedKpisAsync, foundryAgentClient, knowledgeProvider, logger)
    {
    }

    public DivisionRecommendAgent(
        DivisionKind division,
        IFintechDataPlugin dataPlugin,
        IFoundryAgentClient foundryAgentClient,
        IKnowledgeProvider knowledgeProvider,
        ILogger<DivisionRecommendAgent> logger)
        : this(division, dataPlugin, dataPlugin.GetRepresentativeKpisAsync, dataPlugin.GetDetailedKpisAsync, foundryAgentClient, knowledgeProvider, logger)
    {
    }

    private DivisionRecommendAgent(
        DivisionKind division,
        object dataPlugin,
        Func<string, CancellationToken, Task<string>> representativeKpis,
        Func<string, CancellationToken, Task<string>> detailedKpis,
        IFoundryAgentClient foundryAgentClient,
        IKnowledgeProvider knowledgeProvider,
        ILogger<DivisionRecommendAgent> logger)
    {
        Division = division;
        _dataPlugin = dataPlugin;
        _representativeKpis = representativeKpis;
        _detailedKpis = detailedKpis;
        _foundryAgentClient = foundryAgentClient;
        _knowledgeProvider = knowledgeProvider;
        _logger = logger;
    }

    public async Task RunAsync(NewsAnalysisContext ctx, CancellationToken ct)
    {
        _logger.LogInformation("DivisionRecommendAgent invoked for {Division}", Division);
        var scenario = ctx.OriginalNewsText;
        var representativeKpisTask = _representativeKpis(scenario, ct);
        var detailedKpisTask = _detailedKpis(scenario, ct);
        var knowledgeQuery = string.Join('\n', new[]
        {
            ctx.OriginalNewsText,
            ctx.WebResearchResult?.Summary ?? string.Empty,
            string.Join(' ', ctx.WebResearchResult?.KeyFactors ?? []),
            RecommendPrompts.DivisionToken(Division)
        });
        var knowledgeTask = _knowledgeProvider.SearchAsync(knowledgeQuery, maxResults: 6, maxSnippetLength: 1000, ct);
        await Task.WhenAll(representativeKpisTask, detailedKpisTask, knowledgeTask);

        var userMessage = JsonSerializer.Serialize(new
        {
            news_text = ctx.OriginalNewsText,
            web_research = ctx.WebResearchResult,
            impact_result = ctx.ImpactResult,
            target_division = RecommendPrompts.DivisionToken(Division),
            representative_kpis = SafeJson(await representativeKpisTask),
            detailed_kpis = SafeJson(await detailedKpisTask),
            skill_ds_knowledge = FilterKnowledge(await knowledgeTask).Select(item => new { item.Division, item.Topic, item.Snippet }),
            output_schema = SafeJson(RecommendPrompts.OutputJsonSchema)
        }, JsonOptions);

        var response = await _foundryAgentClient.InvokeAsync(
            RecommendPrompts.SystemPrompt(Division),
            userMessage,
            new[] { _dataPlugin },
            ct);

        var recommendation = ParseResultOrFallback(response);
        lock (ctx.Recommendations)
        {
            ctx.Recommendations.Add(recommendation);
        }
    }

    private DivisionRecommendation ParseResultOrFallback(string response)
    {
        try
        {
            using var document = JsonDocument.Parse(ExtractJsonObject(response));
            var root = document.RootElement;
            var headline = root.TryGetProperty("headline", out var headlineElement)
                ? headlineElement.GetString() ?? string.Empty
                : string.Empty;
            var nextActions = ReadStringArray(root, "next_actions")
                .Concat(ReadActionTitles(root, "recommended_actions"))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Take(5)
                .ToArray();
            var dataReferences = ReadStringArray(root, "data_references")
                .Concat(ReadStringArray(root, "data_evidence"))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (string.IsNullOrWhiteSpace(headline) || nextActions.Length == 0)
            {
                return FallbackRecommendation();
            }

            return new DivisionRecommendation(Division, headline, nextActions, dataReferences);
        }
        catch (JsonException)
        {
            return FallbackRecommendation();
        }
    }

    private DivisionRecommendation FallbackRecommendation() =>
        new(Division, "(LLM parse failed)", ["KPI を再確認"], [$"{RecommendPrompts.DivisionToken(Division)}_ai.risk_summary"]);

    private IReadOnlyList<KnowledgeSnippet> FilterKnowledge(IReadOnlyList<KnowledgeSnippet> snippets)
    {
        var divisionToken = RecommendPrompts.DivisionToken(Division);
        var filtered = snippets
            .Where(item =>
                string.Equals(item.Division, divisionToken, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.Division, "common", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return filtered.Length > 0 ? filtered : snippets;
    }

    private static IEnumerable<string> ReadStringArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return array.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(value => !string.IsNullOrWhiteSpace(value))!;
    }

    private static IEnumerable<string> ReadActionTitles(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return array.EnumerateArray().Select(item =>
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                return item.GetString();
            }

            if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("title", out var title))
            {
                return title.GetString();
            }

            return null;
        }).Where(value => !string.IsNullOrWhiteSpace(value))!;
    }

    private static string ExtractJsonObject(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end < start)
        {
            throw new JsonException("No JSON object found in LLM response.");
        }

        return text[start..(end + 1)];
    }

    private static object SafeJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<object>(json, JsonOptions) ?? json;
        }
        catch (JsonException)
        {
            return json;
        }
    }
}
