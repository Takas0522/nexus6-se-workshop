using Azure;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Registry;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace NewsAnalysisAgent.Tools;

public sealed class TeamsWorkflowsPlugin(
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory,
    MockTeamsPlugin mockTeamsPlugin,
    ILogger<TeamsWorkflowsPlugin> logger,
    ResiliencePipelineProvider<string>? resiliencePipelineProvider = null,
    SecretClient? secretClient = null) : ITeamsNotificationPlugin
{
    public async Task<string> SendAsync(string division, string cardPayloadJson, CancellationToken ct = default)
    {
        var url = await ResolveWorkflowUrlAsync(division, ct);
        if (string.IsNullOrWhiteSpace(url))
        {
            logger.LogInformation("Teams Workflows URL is not configured for {Division}; using mock fallback", division);
            return await mockTeamsPlugin.SendAsync(division, cardPayloadJson, ct);
        }

        var pipeline = resiliencePipelineProvider?.GetPipeline("agent-retry");
        if (pipeline is null)
        {
            await PostAsync(url, cardPayloadJson, ct);
        }
        else
        {
            await pipeline.ExecuteAsync(async token => await PostAsync(url, cardPayloadJson, token), ct);
        }

        return $"teams:{division}";
    }

    private async Task<string?> ResolveWorkflowUrlAsync(string division, CancellationToken ct)
    {
        if (secretClient is not null)
        {
            try
            {
                var secret = await secretClient.GetSecretAsync($"Teams--WorkflowsUrl--{division}", cancellationToken: ct);
                if (!string.IsNullOrWhiteSpace(secret.Value.Value))
                {
                    return secret.Value.Value;
                }
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
            {
                logger.LogInformation("Key Vault secret for Teams Workflows URL was not found for {Division}; checking appsettings", division);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Key Vault Teams Workflows URL lookup failed for {Division}; checking appsettings", division);
            }
        }

        return configuration[$"Teams:WorkflowsUrl:{division}"];
    }

    private async Task PostAsync(string url, string payloadJson, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(payloadJson, Encoding.UTF8, "application/json")
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        using var response = await httpClientFactory.CreateClient("teams-workflows").SendAsync(request, ct);
        if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Accepted)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        throw new HttpRequestException($"Teams Workflows POST failed with {(int)response.StatusCode}: {body}");
    }
}
