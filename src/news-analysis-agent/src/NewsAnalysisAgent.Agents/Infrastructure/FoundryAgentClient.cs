using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace NewsAnalysisAgent.Agents.Infrastructure;

public sealed class FoundryAgentClient(
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory,
    TokenCredential credential,
    ILogger<FoundryAgentClient> logger,
    MockFoundryAgentClient fallback) : IFoundryAgentClient
{
    private const string DefaultApiVersion = "2024-10-21";
    private const int DefaultMaxCompletionTokens = 4096;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<string> InvokeAsync(
        string instructions,
        string userMessage,
        IReadOnlyList<object> tools,
        CancellationToken ct = default)
    {
        var projectEndpoint = configuration["Foundry:ProjectEndpoint"];
        var deployment = configuration["Foundry:DefaultModelDeployment"];
        if (string.IsNullOrWhiteSpace(projectEndpoint) || string.IsNullOrWhiteSpace(deployment))
        {
            return await fallback.InvokeAsync(instructions, userMessage, tools, ct);
        }

        try
        {
            var apiVersion = configuration["Foundry:ApiVersion"] ?? DefaultApiVersion;
            var maxCompletionTokens = configuration.GetValue("Foundry:MaxCompletionTokens", DefaultMaxCompletionTokens);
            var endpoint = BuildChatCompletionsEndpoint(projectEndpoint, deployment, apiVersion);
            var token = await credential.GetTokenAsync(
                new TokenRequestContext(["https://cognitiveservices.azure.com/.default"]),
                ct);

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
            request.Content = JsonContent(new
            {
                messages = new object[]
                {
                    new { role = "system", content = instructions },
                    new { role = "user", content = userMessage }
                },
                temperature = 0.3,
                max_completion_tokens = maxCompletionTokens
            });

            var client = httpClientFactory.CreateClient("foundry-agent");
            logger.LogInformation("Invoking Foundry chat completions deployment {Deployment} api-version {ApiVersion}.", deployment, apiVersion);
            using var response = await client.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Foundry chat completions returned {StatusCode} for deployment {Deployment} api-version {ApiVersion}. Error body: {ErrorBody}. Falling back to mock client.",
                    response.StatusCode,
                    deployment,
                    apiVersion,
                    Truncate(body, 1000));
                return await fallback.InvokeAsync(instructions, userMessage, tools, ct);
            }

            var content = ExtractAssistantContent(body);
            return string.IsNullOrWhiteSpace(content)
                ? await fallback.InvokeAsync(instructions, userMessage, tools, ct)
                : content;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Foundry chat completions invocation failed. Falling back to mock client.");
            return await fallback.InvokeAsync(instructions, userMessage, tools, ct);
        }
    }

    private static StringContent JsonContent(object value) =>
        new(JsonSerializer.Serialize(value, JsonOptions), Encoding.UTF8, "application/json");

    private static Uri BuildChatCompletionsEndpoint(string projectEndpoint, string deployment, string apiVersion)
    {
        var baseEndpoint = projectEndpoint.TrimEnd('/');
        var projectSegment = baseEndpoint.IndexOf("/api/projects/", StringComparison.OrdinalIgnoreCase);
        if (projectSegment >= 0)
        {
            baseEndpoint = baseEndpoint[..projectSegment];
        }

        return new Uri($"{baseEndpoint}/openai/deployments/{Uri.EscapeDataString(deployment)}/chat/completions?api-version={Uri.EscapeDataString(apiVersion)}");
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private static string? ExtractAssistantContent(string body)
    {
        using var document = JsonDocument.Parse(body);
        if (document.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
        {
            var first = choices[0];
            if (first.TryGetProperty("message", out var message) &&
                message.TryGetProperty("content", out var content))
            {
                return content.GetString();
            }
        }

        if (document.RootElement.TryGetProperty("output_text", out var outputText))
        {
            return outputText.GetString();
        }

        return null;
    }
}
