using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace NewsAnalysisAgent.Tools;

/// <summary>
/// Web IQ API を使用した検索・ページ取得プラグイン。
/// IBingSearchPlugin (ニュース検索) と IWebPageFetchPlugin (ページ閲覧) の両方を実装。
/// </summary>
public sealed class WebIqPlugin : IBingSearchPlugin, IWebPageFetchPlugin
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TokenCredential _credential;
    private readonly IBingSearchPlugin _fallbackSearch;
    private readonly IWebPageFetchPlugin _fallbackBrowse;
    private readonly ILogger<WebIqPlugin> _logger;

    public WebIqPlugin(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        TokenCredential credential,
        MockBingSearchPlugin fallbackSearch,
        WebPageFetchPlugin fallbackBrowse,
        ILogger<WebIqPlugin> logger)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _credential = credential;
        _fallbackSearch = fallbackSearch;
        _fallbackBrowse = fallbackBrowse;
        _logger = logger;
    }

    private string? BaseUrl => _configuration["WebIq:BaseUrl"];
    private string? ApiKey => _configuration["WebIq:ApiKey"];
    private bool UseEntraId => string.Equals(_configuration["WebIq:AuthMode"], "EntraID", StringComparison.OrdinalIgnoreCase);
    private string Scope => _configuration["WebIq:Scope"] ?? "https://cognitiveservices.azure.com/.default";

    /// <summary>
    /// ニュース検索 (IBingSearchPlugin 実装)。
    /// /search/news エンドポイントを使用。
    /// </summary>
    public async Task<string> SearchAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(BaseUrl))
        {
            return await _fallbackSearch.SearchAsync(query, ct);
        }

        try
        {
            var requestBody = new NewsSearchRequest
            {
                Query = query.Length <= 1000 ? query : query[..1000],
                MaxResults = 5,
                Language = "ja",
                Region = "JP",
                ContentFormat = "text",
                MaxLength = 4000
            };

            var response = await PostAsync("/search/news", requestBody, ct);
            if (!string.IsNullOrWhiteSpace(response))
            {
                return response;
            }

            _logger.LogWarning("WebIQ /search/news returned empty. Falling back.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "WebIQ news search failed. Falling back.");
        }

        return await _fallbackSearch.SearchAsync(query, ct);
    }

    /// <summary>
    /// ページ閲覧 (IWebPageFetchPlugin 実装)。
    /// /browse エンドポイントを使用。
    /// </summary>
    public async Task<string> FetchAsync(string url, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(BaseUrl))
        {
            return await _fallbackBrowse.FetchAsync(url, ct);
        }

        try
        {
            var requestBody = new BrowseRequest
            {
                Url = url,
                MaxLength = 10000,
                LiveCrawl = "fallback",
                Language = "ja",
                Region = "JP",
                ContentFormat = "text"
            };

            var response = await PostAsync("/browse", requestBody, ct);
            if (!string.IsNullOrWhiteSpace(response))
            {
                var browseResult = JsonSerializer.Deserialize<BrowseResponse>(response, JsonOptions);
                return browseResult?.Content ?? string.Empty;
            }

            _logger.LogWarning("WebIQ /browse returned empty for {Url}. Falling back.", url);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "WebIQ browse failed for {Url}. Falling back.", url);
        }

        return await _fallbackBrowse.FetchAsync(url, ct);
    }

    /// <summary>
    /// Web検索 (/search/web)。WebResearchAgent から直接利用可能。
    /// </summary>
    public async Task<string> WebSearchAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(BaseUrl))
        {
            return await _fallbackSearch.SearchAsync(query, ct);
        }

        var requestBody = new WebSearchRequest
        {
            Query = query.Length <= 1000 ? query : query[..1000],
            MaxResults = 5,
            Language = "ja",
            Region = "JP",
            ContentFormat = "text",
            MaxLength = 4000
        };

        return await PostAsync("/search/web", requestBody, ct);
    }

    private async Task<string> PostAsync<T>(string path, T body, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("webiq");
        var endpoint = new Uri($"{BaseUrl!.TrimEnd('/')}{path}");

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Content = new StringContent(
            JsonSerializer.Serialize(body, JsonOptions),
            Encoding.UTF8,
            "application/json");

        // 認証
        if (UseEntraId)
        {
            var token = await _credential.GetTokenAsync(
                new TokenRequestContext([Scope]),
                ct);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        }
        else if (!string.IsNullOrWhiteSpace(ApiKey))
        {
            request.Headers.Add("x-apikey", ApiKey);
        }

        using var response = await client.SendAsync(request, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (response.IsSuccessStatusCode)
        {
            return responseBody;
        }

        _logger.LogWarning("WebIQ {Path} returned {StatusCode}: {Body}",
            path, (int)response.StatusCode, responseBody.Length > 200 ? responseBody[..200] : responseBody);
        return string.Empty;
    }

    // --- Request/Response DTOs ---

    private sealed class NewsSearchRequest
    {
        [JsonPropertyName("query")]
        public string Query { get; set; } = "";

        [JsonPropertyName("maxResults")]
        public int? MaxResults { get; set; }

        [JsonPropertyName("language")]
        public string? Language { get; set; }

        [JsonPropertyName("region")]
        public string? Region { get; set; }

        [JsonPropertyName("contentFormat")]
        public string? ContentFormat { get; set; }

        [JsonPropertyName("maxLength")]
        public int? MaxLength { get; set; }
    }

    private sealed class WebSearchRequest
    {
        [JsonPropertyName("query")]
        public string Query { get; set; } = "";

        [JsonPropertyName("maxResults")]
        public int? MaxResults { get; set; }

        [JsonPropertyName("language")]
        public string? Language { get; set; }

        [JsonPropertyName("region")]
        public string? Region { get; set; }

        [JsonPropertyName("contentFormat")]
        public string? ContentFormat { get; set; }

        [JsonPropertyName("maxLength")]
        public int? MaxLength { get; set; }
    }

    private sealed class BrowseRequest
    {
        [JsonPropertyName("url")]
        public string Url { get; set; } = "";

        [JsonPropertyName("maxLength")]
        public int? MaxLength { get; set; }

        [JsonPropertyName("liveCrawl")]
        public string? LiveCrawl { get; set; }

        [JsonPropertyName("language")]
        public string? Language { get; set; }

        [JsonPropertyName("region")]
        public string? Region { get; set; }

        [JsonPropertyName("contentFormat")]
        public string? ContentFormat { get; set; }
    }

    private sealed class BrowseResponse
    {
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("content")]
        public string? Content { get; set; }

        [JsonPropertyName("retryAfter")]
        public string? RetryAfter { get; set; }
    }
}
