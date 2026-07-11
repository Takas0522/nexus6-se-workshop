using System.Text.Json;
using Microsoft.Extensions.Logging;
using NewsAnalysisAgent.Agents.Infrastructure;
using NewsAnalysisAgent.Agents.Infrastructure.Knowledge;
using NewsAnalysisAgent.Models;
using NewsAnalysisAgent.Tools;

namespace NewsAnalysisAgent.Agents.DivisionRecommend;

/// <summary>
/// DivisionConfig (Blob 設定) ベースの事業部レコメンドエージェント。
/// 既存の DivisionRecommendAgent と同じワークフローだが、
/// DivisionKind enum ではなく DivisionConfig を使用する。
/// </summary>
public sealed class ConfigDrivenDivisionRecommendAgent : IWorkflowStep<NewsAnalysisContext>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly DivisionConfig _config;
    private readonly IDivisionDataPlugin? _dataPlugin;
    private readonly IFoundryAgentClient _foundryAgentClient;
    private readonly IKnowledgeProvider _knowledgeProvider;
    private readonly ILogger _logger;

    public ConfigDrivenDivisionRecommendAgent(
        DivisionConfig config,
        IDivisionDataPlugin? dataPlugin,
        IFoundryAgentClient foundryAgentClient,
        IKnowledgeProvider knowledgeProvider,
        ILogger logger)
    {
        _config = config;
        _dataPlugin = dataPlugin;
        _foundryAgentClient = foundryAgentClient;
        _knowledgeProvider = knowledgeProvider;
        _logger = logger;
    }

    public async Task RunAsync(NewsAnalysisContext ctx, CancellationToken ct)
    {
        _logger.LogInformation("ConfigDrivenDivisionRecommendAgent invoked for {Division}", _config.Id);

        var scenario = ctx.OriginalNewsText;

        // KPI 取得 (plugin が null なら空)
        var representativeKpis = _dataPlugin != null
            ? await _dataPlugin.GetRepresentativeKpisAsync(scenario, ct)
            : "{}";
        var detailedKpis = _dataPlugin != null
            ? await _dataPlugin.GetDetailedKpisAsync(scenario, ct)
            : "{}";

        // Knowledge 検索
        var knowledgeQuery = string.Join('\n', new[]
        {
            ctx.OriginalNewsText,
            ctx.WebResearchResult?.Summary ?? string.Empty,
            string.Join(' ', ctx.WebResearchResult?.KeyFactors ?? []),
            _config.Id
        });
        var knowledge = await _knowledgeProvider.SearchAsync(knowledgeQuery, maxResults: 6, maxSnippetLength: 1000, ct);

        var filteredKnowledge = knowledge
            .Where(item =>
                string.Equals(item.Division, _config.Id, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.Division, "common", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (filteredKnowledge.Length == 0) filteredKnowledge = knowledge.ToArray();

        var userMessage = JsonSerializer.Serialize(new
        {
            news_text = ctx.OriginalNewsText,
            web_research = ctx.WebResearchResult,
            impact_result = ctx.ImpactResult,
            target_division = _config.Id,
            representative_kpis = SafeJson(representativeKpis),
            detailed_kpis = SafeJson(detailedKpis),
            skill_ds_knowledge = filteredKnowledge.Select(item => new { item.Division, item.Topic, item.Snippet }),
            output_schema = SafeJson(ConfigDrivenRecommendPrompts.OutputJsonSchema)
        }, JsonOptions);

        var systemPrompt = ConfigDrivenRecommendPrompts.SystemPrompt(_config);

        var response = await _foundryAgentClient.InvokeAsync(
            systemPrompt,
            userMessage,
            _dataPlugin != null ? new object[] { _dataPlugin } : [],
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
            var headline = root.TryGetProperty("headline", out var h) ? h.GetString() ?? "" : "";
            var nextActions = ReadNextActions(root).Take(5).ToArray();
            var dataReferences = ReadStringArray(root, "data_references").ToArray();
            var sourceFiles = ReadStringArray(root, "source_files").ToArray();
            var kpiReferences = ReadKpiReferences(root).ToArray();

            if (string.IsNullOrWhiteSpace(headline) || nextActions.Length == 0)
                return FallbackRecommendation();

            return new DivisionRecommendation(_config.Id, headline, nextActions, dataReferences)
            {
                SourceFiles = sourceFiles,
                KpiReferences = kpiReferences
            };
        }
        catch (JsonException)
        {
            return FallbackRecommendation();
        }
    }

    private DivisionRecommendation FallbackRecommendation() =>
        new(_config.Id, "(LLM parse failed)",
            [new NextAction("KPI を再確認", "対象 KPI と Skill/DS 根拠を再確認する。")],
            [$"{_config.FabricTable}"]);

    private static string ExtractJsonObject(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start >= 0 && end > start) return text[start..(end + 1)];
        return text;
    }

    private static IEnumerable<NextAction> ReadNextActions(JsonElement root)
    {
        foreach (var propName in new[] { "next_actions", "recommended_actions" })
        {
            if (!root.TryGetProperty(propName, out var array) || array.ValueKind != JsonValueKind.Array)
                continue;
            foreach (var item in array.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    yield return NextAction.FromText(item.GetString() ?? "");
                }
                else if (item.ValueKind == JsonValueKind.Object)
                {
                    var title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                    var body = item.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
                    yield return new NextAction(title, body);
                }
            }
        }
    }

    private static IEnumerable<string> ReadStringArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var array) || array.ValueKind != JsonValueKind.Array)
            yield break;
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                yield return item.GetString()!;
        }
    }

    private static IEnumerable<KpiReference> ReadKpiReferences(JsonElement root)
    {
        if (!root.TryGetProperty("kpi_references", out var array) || array.ValueKind != JsonValueKind.Array)
            yield break;
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            yield return new KpiReference(
                item.TryGetProperty("physical_name", out var pn) ? pn.GetString() ?? "" : "",
                item.TryGetProperty("value", out var v) ? v.GetString() ?? "" : "",
                item.TryGetProperty("unit", out var u) ? u.GetString() ?? "" : "",
                item.TryGetProperty("table", out var tb) ? tb.GetString() ?? "" : "");
        }
    }

    private static object? SafeJson(string json)
    {
        try { return JsonSerializer.Deserialize<object>(json); }
        catch { return json; }
    }
}
