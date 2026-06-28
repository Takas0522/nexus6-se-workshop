using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.Storage.Queues;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace NewsPortalTriggerFunction;

public sealed class NewsPortalPollerFunction(
    IConfiguration configuration,
    NewsPortalCrawler crawler,
    QueueClient queueClient,
    ILogger<NewsPortalPollerFunction> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Function("NewsPortalPoller")]
    public async Task RunAsync([TimerTrigger("%DailyRunCron%")] TimerInfo timer, CancellationToken ct)
    {
        var articles = await crawler.FetchArticlesAsync(ct);
        logger.LogInformation("News portal crawl completed. Candidates: {Count}", articles.Count);

        if (articles.Count == 0)
        {
            return;
        }

        var maxEnqueue = configuration.GetValue("NewsPortal__MaxEnqueuePerRun", 10);
        var enqueued = 0;
        foreach (var article in articles.Take(maxEnqueue))
        {
            var payload = new NewsAnalysisJob(
                article.Text,
                article.Url,
                article.PublishedAt,
                SearchHints: []);

            var json = JsonSerializer.Serialize(payload, JsonOptions);
            await queueClient.SendMessageAsync(BinaryData.FromString(json), cancellationToken: ct);
            enqueued++;
        }

        logger.LogInformation("Enqueued {Count} jobs to queue {QueueUri}", enqueued, queueClient.Uri);
    }
}

public sealed class NewsPortalCrawler(
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory,
    ILogger<NewsPortalCrawler> logger)
{
    private static readonly Regex LinkRegex = new("href\\s*=\\s*\"(?<href>[^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TagRegex = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex WhiteSpaceRegex = new("\\s+", RegexOptions.Compiled);

    public async Task<IReadOnlyList<PortalArticle>> FetchArticlesAsync(CancellationToken ct)
    {
        var baseUrl = configuration["NewsPortal__BaseUrl"] ?? "https://stnexus6portal1t2i.z1.web.core.windows.net/";
        var articlePathPrefix = configuration["NewsPortal__ArticlePathPrefix"] ?? "article-";
        using var client = httpClientFactory.CreateClient();
        var html = await client.GetStringAsync(baseUrl, ct);
        var baseUri = new Uri(baseUrl);
        var links = LinkRegex.Matches(html)
            .Select(m => WebUtility.HtmlDecode(m.Groups["href"].Value))
            .Where(href => href.Contains(articlePathPrefix, StringComparison.OrdinalIgnoreCase))
            .Select(href => href.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? href : new Uri(baseUri, href).ToString())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var results = new List<PortalArticle>();
        foreach (var link in links)
        {
            try
            {
                var articleHtml = await client.GetStringAsync(link, ct);
                var text = ExtractText(articleHtml);
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                results.Add(new PortalArticle(link, text, DateTimeOffset.UtcNow));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to fetch article: {Url}", link);
            }
        }

        return results;
    }

    private static string ExtractText(string html)
    {
        var withoutTags = TagRegex.Replace(html, " ");
        return WhiteSpaceRegex.Replace(WebUtility.HtmlDecode(withoutTags), " ").Trim();
    }
}

public sealed record PortalArticle(string Url, string Text, DateTimeOffset PublishedAt);

public sealed record NewsAnalysisJob(
    string OriginalNewsText,
    string SourceUrl,
    DateTimeOffset PublishedAt,
    string[] SearchHints);
