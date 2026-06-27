using System.Text.Json;
using NewsAnalysisAgent.Agents.Infrastructure;
using NewsAnalysisAgent.Models;
using NewsAnalysisAgent.Tools;
using Microsoft.Extensions.Logging;

namespace NewsAnalysisAgent.Agents.WebResearch;

public sealed class WebResearchAgent(
    IFoundryAgentClient foundryAgentClient,
    IBingSearchPlugin bingSearchPlugin,
    IWebPageFetchPlugin webPageFetchPlugin,
    ILogger<WebResearchAgent> logger) : IWorkflowStep<NewsAnalysisContext>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task RunAsync(NewsAnalysisContext ctx, CancellationToken ct)
    {
        var searchQuery = BuildSearchQuery(ctx.OriginalNewsText, ctx.SearchHints);
        logger.LogInformation("WebResearchAgent invoked for query length {Length}", searchQuery.Length);

        var searchJson = await bingSearchPlugin.SearchAsync(searchQuery, ct);
        var candidateUrls = ExtractUrls(searchJson).Take(3).ToArray();
        var fetchedPages = await FetchPagesAsync(candidateUrls, ct);

        var userMessage = JsonSerializer.Serialize(new
        {
            news_text = ctx.OriginalNewsText,
            search_hints = ctx.SearchHints,
            search_results = SafeJson(searchJson),
            fetched_pages = fetchedPages,
            output_schema = SafeJson(WebResearchPrompts.OutputJsonSchema)
        }, JsonOptions);

        var response = await foundryAgentClient.InvokeAsync(
            WebResearchPrompts.SystemPrompt,
            userMessage,
            new object[] { bingSearchPlugin, webPageFetchPlugin },
            ct);

        ctx.WebResearchResult = ParseResultOrFallback(response);
    }

    private static string BuildSearchQuery(string newsText, IReadOnlyList<string> searchHints)
    {
        var hints = searchHints.Where(hint => !string.IsNullOrWhiteSpace(hint));
        var seed = string.Join(' ', hints.Append(newsText));
        return seed.Length <= 300 ? seed : seed[..300];
    }

    private async Task<IReadOnlyList<object>> FetchPagesAsync(IReadOnlyList<string> urls, CancellationToken ct)
    {
        var pages = new List<object>();
        foreach (var url in urls)
        {
            try
            {
                var text = await webPageFetchPlugin.FetchAsync(url, ct);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    pages.Add(new { url, text = text.Length <= 4000 ? text : text[..4000] });
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Failed to fetch web page {Url}", url);
            }
        }

        return pages;
    }

    private static WebResearchResult ParseResultOrFallback(string response)
    {
        try
        {
            using var document = JsonDocument.Parse(ExtractJsonObject(response));
            var root = document.RootElement;
            var summary = root.TryGetProperty("summary", out var summaryElement)
                ? summaryElement.GetString() ?? string.Empty
                : string.Empty;
            var keyFactors = ReadStringArray(root, "key_factors").Take(10).ToArray();
            var sourceUrls = ReadStringArray(root, "source_urls")
                .Where(ReferenceCatalog.IsAllowedWebReference)
                .ToArray();

            return new WebResearchResult(TrimSummary(summary), keyFactors, sourceUrls);
        }
        catch (JsonException)
        {
            var fallbackSummary = $"(parse-failed) {response}";
            return new WebResearchResult(TrimSummary(fallbackSummary), [], []);
        }
    }

    private static string TrimSummary(string summary) => summary.Length <= 500 ? summary : summary[..500];

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

    private static IEnumerable<string> ExtractUrls(string json)
    {
        using var document = JsonDocument.Parse(json);
        return ExtractUrls(document.RootElement).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IEnumerable<string> ExtractUrls(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    foreach (var url in ExtractUrls(property.Value))
                    {
                        yield return url;
                    }
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    foreach (var url in ExtractUrls(item))
                    {
                        yield return url;
                    }
                }
                break;
            case JsonValueKind.String:
                var value = element.GetString();
                if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
                    (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
                    ReferenceCatalog.IsAllowedWebReference(uri.ToString()))
                {
                    yield return uri.ToString();
                }
                break;
        }
    }
}
