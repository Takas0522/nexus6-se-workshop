using System.Text.Json;
using NewsAnalysisAgent.Agents.Infrastructure;
using NewsAnalysisAgent.Agents.Infrastructure.Knowledge;
using NewsAnalysisAgent.Models;
using NewsAnalysisAgent.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NewsAnalysisAgent.Agents.BusinessImpact;

public sealed class BusinessImpactAgent(
    IFoundryAgentClient foundryAgentClient,
    IFabricDataPlugin fabricDataPlugin,
    IKnowledgeProvider knowledgeProvider,
    IOptions<DivisionsConfig> divisionsConfig,
    ILogger<BusinessImpactAgent> logger) : IWorkflowStep<NewsAnalysisContext>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task RunAsync(NewsAnalysisContext ctx, CancellationToken ct)
    {
        var yearMonth = ctx.StartedAt.ToUniversalTime().ToString("yyyy-MM");
        logger.LogInformation("BusinessImpactAgent invoked for {YearMonth}. Web research present: {HasWebResearch}", yearMonth, ctx.WebResearchResult is not null);

        var monthlyRevenueJson = await fabricDataPlugin.GetMonthlyRevenueAsync(yearMonth, ct);
        var divisionIds = divisionsConfig.Value.Divisions.Select(d => d.Id).ToArray();
        var divisionSnapshots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var division in divisionIds)
        {
            divisionSnapshots[division] = await fabricDataPlugin.GetDivisionKpiSnapshotAsync(division, yearMonth, ct);
        }

        var knowledgeQuery = string.Join('\n', new[]
        {
            ctx.OriginalNewsText,
            ctx.WebResearchResult?.Summary ?? string.Empty,
            string.Join(' ', ctx.WebResearchResult?.KeyFactors ?? [])
        });
        var knowledge = await knowledgeProvider.SearchAsync(knowledgeQuery, maxResults: 6, maxSnippetLength: 1000, ct);

        var userMessage = JsonSerializer.Serialize(new
        {
            news_text = ctx.OriginalNewsText,
            web_research = ctx.WebResearchResult,
            fabric_kpi = new
            {
                monthly_revenue = SafeJson(monthlyRevenueJson),
                division_snapshots = divisionSnapshots.ToDictionary(pair => pair.Key, pair => SafeJson(pair.Value), StringComparer.OrdinalIgnoreCase)
            },
            knowledge_snippets = knowledge.Select(item => new { item.Division, item.Topic, item.Snippet }),
            output_schema = SafeJson(BusinessImpactPrompts.BuildOutputJsonSchema(divisionsConfig.Value.Divisions))
        }, JsonOptions);

        var response = await foundryAgentClient.InvokeAsync(
            BusinessImpactPrompts.BuildSystemPrompt(divisionsConfig.Value.Divisions),
            userMessage,
            new object[] { fabricDataPlugin },
            ct);

        ctx.ImpactResult = ParseResultOrFallback(response, monthlyRevenueJson, divisionSnapshots);
    }

    private BusinessImpactResult ParseResultOrFallback(
        string response,
        string monthlyRevenueJson,
        IReadOnlyDictionary<string, string> divisionSnapshots)
    {
        try
        {
            using var document = JsonDocument.Parse(ExtractJsonObject(response));
            var root = document.RootElement;
            var scores = ReadImpactScores(root).ToArray();
            var expectedCount = divisionsConfig.Value.Divisions.Count;
            if (scores.Length != expectedCount || scores.Select(static score => score.Division).Distinct().Count() != expectedCount)
            {
                throw new JsonException($"Impact score output must contain all {expectedCount} divisions exactly once.");
            }

            scores = scores.OrderByDescending(static score => score.Score).ToArray();
            var reasons = ReadStringArray(root, "impact_reasons").Take(3).ToArray();
            if (reasons.Length == 0)
            {
                reasons = scores.Select(score => $"{score.Division}: LLM score {score.Score:0.0} ({score.RiskLevel}).").ToArray();
            }

            var dataReferences = ReadStringArray(root, "data_references")
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var sourceFiles = ReadStringArray(root, "source_files")
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var kpiReferences = ReadKpiReferences(root).ToArray();

            return new BusinessImpactResult(scores, reasons, scores.Select(static score => score.Division).ToArray())
            {
                DataReferences = dataReferences,
                SourceFiles = sourceFiles,
                KpiReferences = kpiReferences
            };
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to parse BusinessImpactAgent response. Falling back to KPI heuristic.");
            return BuildHeuristicFallback(monthlyRevenueJson, divisionSnapshots);
        }
    }

    private static IEnumerable<ImpactScore> ReadImpactScores(JsonElement root)
    {
        if (!root.TryGetProperty("impact_scores", out var array) || array.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var item in array.EnumerateArray())
        {
            if (!item.TryGetProperty("division", out var divisionElement))
            {
                continue;
            }
            var division = divisionElement.GetString();
            if (string.IsNullOrWhiteSpace(division))
            {
                continue;
            }

            var score = item.TryGetProperty("score", out var scoreElement) && scoreElement.TryGetDouble(out var value)
                ? Math.Clamp(value, 0, 5)
                : 0;
            var riskLevel = item.TryGetProperty("risk_level", out var riskElement)
                ? NormalizeRiskLevel(riskElement.GetString(), score)
                : RiskLevelFromScore(score);
            yield return new ImpactScore(division, score, riskLevel);
        }
    }

    private static BusinessImpactResult BuildHeuristicFallback(string monthlyRevenueJson, IReadOnlyDictionary<string, string> divisionSnapshots)
    {
        var metrics = ReadRevenueMetrics(monthlyRevenueJson);
        var divisions = metrics.Keys.Any()
            ? metrics.Keys.ToArray()
            : divisionSnapshots.Keys.Any()
                ? divisionSnapshots.Keys.ToArray()
                : ["division1", "division2", "division3"];
        var scores = divisions
            .Select(division => BuildHeuristicScore(division, metrics.GetValueOrDefault(division), divisionSnapshots.GetValueOrDefault(division)))
            .OrderByDescending(static score => score.Score)
            .ToArray();

        var reasons = scores.Select(score => $"{score.Division}: Fabric KPI heuristic fallback score {score.Score:0.0} based on margin, churn, FX exposure, and risk summary volume.").ToArray();
        return new BusinessImpactResult(scores, reasons, scores.Select(static score => score.Division).ToArray());
    }

    private static ImpactScore BuildHeuristicScore(string division, RevenueMetric? metric, string? snapshotJson)
    {
        var score = 1.0;
        if (metric is not null)
        {
            score += metric.GrossMarginRate < 0.28m ? 1.2 : metric.GrossMarginRate < 0.35m ? 0.7 : 0.3;
            score += metric.GrossRevenueJpy > 0 ? Math.Min(1.2, (double)((metric.FxExposureUsd * 150m + metric.FxExposureOtherJpy) / metric.GrossRevenueJpy) * 4) : 0;
            score += metric.ActiveCustomerCount > 0 ? Math.Min(1.0, (double)metric.ChurnedCustomerCount / metric.ActiveCustomerCount * 20) : 0;
        }

        if (!string.IsNullOrWhiteSpace(snapshotJson) && CountMetricRows(snapshotJson) >= 2)
        {
            score += 0.4;
        }

        score += 0.1; // base bonus

        var clamped = Math.Round(Math.Clamp(score, 0, 5), 1);
        return new ImpactScore(division, clamped, RiskLevelFromScore(clamped));
    }

    private static Dictionary<string, RevenueMetric> ReadRevenueMetrics(string json)
    {
        var result = new Dictionary<string, RevenueMetric>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var document = JsonDocument.Parse(ExtractJsonObject(json));
            if (!document.RootElement.TryGetProperty("rows", out var rows) || rows.ValueKind != JsonValueKind.Array)
            {
                return result;
            }

            foreach (var row in rows.EnumerateArray())
            {
                if (!row.TryGetProperty("division", out var divisionElement))
                {
                    continue;
                }
                var division = divisionElement.GetString();
                if (string.IsNullOrWhiteSpace(division))
                {
                    continue;
                }

                result[division] = new RevenueMetric(
                    GetDecimal(row, "gross_revenue_jpy"),
                    GetDecimal(row, "gross_margin_rate"),
                    GetDecimal(row, "fx_exposure_usd"),
                    GetDecimal(row, "fx_exposure_other_jpy"),
                    GetInt(row, "active_customer_count"),
                    GetInt(row, "churned_customer_count"));
            }
        }
        catch (JsonException)
        {
            return result;
        }

        return result;
    }

    private static int CountMetricRows(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(ExtractJsonObject(json));
            return document.RootElement.TryGetProperty("rows", out var rows) && rows.ValueKind == JsonValueKind.Array
                ? rows.GetArrayLength()
                : 0;
        }
        catch (JsonException)
        {
            return 0;
        }
    }

    private static decimal GetDecimal(JsonElement row, string propertyName) =>
        row.TryGetProperty(propertyName, out var value) && value.TryGetDecimal(out var number) ? number : 0;

    private static int GetInt(JsonElement row, string propertyName) =>
        row.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out var number) ? number : 0;

    private static IEnumerable<string> ReadStringArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return array.EnumerateArray()
            .Where(static item => item.ValueKind == JsonValueKind.String)
            .Select(static item => item.GetString())
            .Where(static value => !string.IsNullOrWhiteSpace(value))!;
    }

    private static IEnumerable<KpiReference> ReadKpiReferences(JsonElement root)
    {
        if (!root.TryGetProperty("kpi_references", out var array) || array.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var item in array.EnumerateArray().Where(static item => item.ValueKind == JsonValueKind.Object))
        {
            var physicalName = ReadString(item, "physical_name") ?? ReadString(item, "name");
            if (string.IsNullOrWhiteSpace(physicalName))
            {
                continue;
            }

            yield return ReferenceCatalog.LocalizeKpi(new KpiReference(
                physicalName,
                LogicalNameJa: ReadString(item, "logical_name_ja") ?? string.Empty,
                Value: ReadString(item, "value"),
                Unit: ReadString(item, "unit"),
                Table: ReadString(item, "table")));
        }
    }

    private static string? ReadString(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var element))
        {
            return null;
        }

        return element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString();
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

    private static string NormalizeRiskLevel(string? riskLevel, double score)
    {
        var normalized = riskLevel?.Trim().ToLowerInvariant();
        return normalized is "low" or "medium" or "high" ? normalized : RiskLevelFromScore(score);
    }

    private static string RiskLevelFromScore(double score) => score switch
    {
        >= 3.5 => "high",
        >= 2.0 => "medium",
        _ => "low"
    };

    private sealed record RevenueMetric(
        decimal GrossRevenueJpy,
        decimal GrossMarginRate,
        decimal FxExposureUsd,
        decimal FxExposureOtherJpy,
        int ActiveCustomerCount,
        int ChurnedCustomerCount);
}
