using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace NewsAnalysisAgent.Tools;

public interface IBingSearchPlugin
{
    Task<string> SearchAsync(string query, CancellationToken ct = default);
}

public sealed class BingSearchPlugin(
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory,
    TokenCredential credential,
    MockBingSearchPlugin fallback,
    ILogger<BingSearchPlugin> logger) : IBingSearchPlugin
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<string> SearchAsync(string query, CancellationToken ct = default)
    {
        var connectionId = configuration["Foundry:GroundingBingConnectionId"];
        var projectEndpoint = configuration["Foundry:ProjectEndpoint"];
        if (string.IsNullOrWhiteSpace(connectionId) || string.IsNullOrWhiteSpace(projectEndpoint))
        {
            return await fallback.SearchAsync(query, ct);
        }

        try
        {
            var endpoint = new Uri($"{projectEndpoint.TrimEnd('/')}/connections/{Uri.EscapeDataString(connectionId)}:search?api-version=2025-04-01-preview");
            var token = await credential.GetTokenAsync(
                new TokenRequestContext(["https://cognitiveservices.azure.com/.default"]),
                ct);

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
            request.Content = new StringContent(
                JsonSerializer.Serialize(new { query, top = 5 }, JsonOptions),
                Encoding.UTF8,
                "application/json");

            var client = httpClientFactory.CreateClient("bing-grounding");
            using var response = await client.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (response.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(body))
            {
                return body;
            }

            logger.LogWarning("Foundry Grounding with Bing returned {StatusCode}. Falling back to mock search.", response.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Foundry Grounding with Bing search failed. Falling back to mock search.");
        }

        return await fallback.SearchAsync(query, ct);
    }
}

public sealed class MockBingSearchPlugin : IBingSearchPlugin
{
    public Task<string> SearchAsync(string query, CancellationToken ct = default) =>
        Task.FromResult(JsonSerializer.Serialize(new
        {
            query,
            summary = "mock bing search result",
            source_urls = new[] { "https://example.com/mock-news", "https://example.com/mock-market" },
            results = new[]
            {
                new { title = "Mock market background", url = "https://example.com/mock-news" },
                new { title = "Mock competitor trend", url = "https://example.com/mock-market" }
            }
        }));
}
