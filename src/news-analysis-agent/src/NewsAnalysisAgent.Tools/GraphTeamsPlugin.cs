using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Azure;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly.Registry;

namespace NewsAnalysisAgent.Tools;

public sealed class GraphTeamsPlugin(
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory,
    MockTeamsPlugin mockTeamsPlugin,
    ILogger<GraphTeamsPlugin> logger,
    ResiliencePipelineProvider<string>? resiliencePipelineProvider = null,
    SecretClient? secretClient = null) : ITeamsNotificationPlugin
{
    private const string GraphScope = "https://graph.microsoft.com/ChannelMessage.Send https://graph.microsoft.com/Group.Read.All offline_access";
    private const string ClientIdSecretName = "Teams--Graph--ClientId";
    private const string TenantIdSecretName = "Teams--Graph--TenantId";
    private const string RefreshTokenSecretName = "Teams--Graph--RefreshToken";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<string> SendAsync(string division, string cardPayloadJson, CancellationToken ct = default)
    {
        var target = ResolveTarget(division);
        if (target is null)
        {
            logger.LogWarning("Teams Graph target is not configured for {Division}; using mock fallback", division);
            return await mockTeamsPlugin.SendAsync(division, cardPayloadJson, ct);
        }

        try
        {
            var pipeline = resiliencePipelineProvider?.GetPipeline("agent-retry");
            if (pipeline is null)
            {
                await PostAsync(target.Value, cardPayloadJson, ct);
            }
            else
            {
                await pipeline.ExecuteAsync(async token => await PostAsync(target.Value, cardPayloadJson, token), ct);
            }

            return $"teams-graph:{division}";
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Teams Graph delegated notification failed for {Division}; using mock fallback", division);
            return await mockTeamsPlugin.SendAsync(division, cardPayloadJson, ct);
        }
    }

    private TeamsGraphTarget? ResolveTarget(string division)
    {
        var section = configuration.GetSection($"Teams:Graph:{division}");
        var teamId = section["TeamId"];
        var channelId = section["ChannelId"];
        return string.IsNullOrWhiteSpace(teamId) || string.IsNullOrWhiteSpace(channelId)
            ? null
            : new TeamsGraphTarget(teamId, channelId);
    }

    private async Task PostAsync(TeamsGraphTarget target, string cardPayloadJson, CancellationToken ct)
    {
        var token = await RefreshDelegatedTokenAsync(ct);
        var requestUri = $"https://graph.microsoft.com/v1.0/teams/{Uri.EscapeDataString(target.TeamId)}/channels/{Uri.EscapeDataString(target.ChannelId)}/messages";
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = new StringContent(BuildChatMessageJson(cardPayloadJson), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await httpClientFactory.CreateClient("teams-graph").SendAsync(request, ct);
        if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created or HttpStatusCode.Accepted)
        {
            logger.LogInformation("Teams Graph POST succeeded for team {TeamId} channel {ChannelId} with status {StatusCode}",
                target.TeamId,
                target.ChannelId,
                (int)response.StatusCode);
            return;
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        throw new HttpRequestException($"Teams Graph POST failed with {(int)response.StatusCode}: {body}", null, response.StatusCode);
    }

    private async Task<string> RefreshDelegatedTokenAsync(CancellationToken ct)
    {
        var clientId = await ResolveSecretOrConfigurationAsync(ClientIdSecretName, "Teams:Graph:ClientId", ct);
        var tenantId = await ResolveSecretOrConfigurationAsync(TenantIdSecretName, "Teams:Graph:TenantId", ct);
        var refreshToken = await ResolveSecretOrConfigurationAsync(RefreshTokenSecretName, "Teams:Graph:RefreshToken", ct);
        if (string.IsNullOrWhiteSpace(clientId) ||
            string.IsNullOrWhiteSpace(tenantId) ||
            string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new InvalidOperationException("Teams Graph delegated credentials are not configured in Key Vault or appsettings.");
        }

        var tokenUri = $"https://login.microsoftonline.com/{Uri.EscapeDataString(tenantId)}/oauth2/v2.0/token";
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["scope"] = GraphScope
        });
        using var response = await httpClientFactory.CreateClient("teams-graph").PostAsync(tokenUri, content, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Teams Graph delegated token refresh failed with {(int)response.StatusCode}: {body}", null, response.StatusCode);
        }

        var token = JsonSerializer.Deserialize<GraphTokenResponse>(body, JsonOptions)
            ?? throw new InvalidOperationException("Teams Graph delegated token refresh returned an empty response.");
        if (string.IsNullOrWhiteSpace(token.AccessToken))
        {
            throw new InvalidOperationException("Teams Graph delegated token refresh did not return an access token.");
        }

        if (!string.IsNullOrWhiteSpace(token.RefreshToken) &&
            !string.Equals(refreshToken, token.RefreshToken, StringComparison.Ordinal) &&
            secretClient is not null)
        {
            await secretClient.SetSecretAsync(RefreshTokenSecretName, token.RefreshToken, ct);
        }

        return token.AccessToken;
    }

    private async Task<string?> ResolveSecretOrConfigurationAsync(string secretName, string configurationKey, CancellationToken ct)
    {
        if (secretClient is not null)
        {
            try
            {
                var secret = await secretClient.GetSecretAsync(secretName, cancellationToken: ct);
                if (!string.IsNullOrWhiteSpace(secret.Value.Value))
                {
                    return secret.Value.Value;
                }
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
            {
                logger.LogInformation("Key Vault secret {SecretName} was not found; checking appsettings", secretName);
            }
        }

        return configuration[configurationKey];
    }

    private static string BuildChatMessageJson(string cardPayloadJson)
    {
        var attachments = new List<object>();
        var contentBuilder = new StringBuilder();
        var fallbackText = string.Empty;

        try
        {
            using var doc = JsonDocument.Parse(cardPayloadJson);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("attachments", out var atts) &&
                atts.ValueKind == JsonValueKind.Array)
            {
                foreach (var att in atts.EnumerateArray())
                {
                    var contentType = att.TryGetProperty("contentType", out var ct) ? ct.GetString() : null;
                    if (att.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Object)
                    {
                        var id = Guid.NewGuid().ToString();
                        attachments.Add(new
                        {
                            id,
                            contentType = contentType ?? "application/vnd.microsoft.card.adaptive",
                            contentUrl = (string?)null,
                            content = content.GetRawText(),
                            name = (string?)null,
                            thumbnailUrl = (string?)null
                        });
                        contentBuilder.Append($"<attachment id=\"{id}\"></attachment>");
                    }
                }
            }

            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("summary", out var s) && s.ValueKind == JsonValueKind.String)
            {
                fallbackText = s.GetString() ?? string.Empty;
            }
        }
        catch (JsonException)
        {
            // fall through to plain rendering
        }

        if (attachments.Count == 0)
        {
            var html = "<p>Web Pulse Recommender notification</p><pre>" + WebUtility.HtmlEncode(cardPayloadJson) + "</pre>";
            return JsonSerializer.Serialize(new
            {
                body = new { contentType = "html", content = html }
            }, JsonOptions);
        }

        return JsonSerializer.Serialize(new
        {
            body = new
            {
                contentType = "html",
                content = string.IsNullOrEmpty(fallbackText)
                    ? contentBuilder.ToString()
                    : WebUtility.HtmlEncode(fallbackText) + contentBuilder.ToString()
            },
            attachments
        }, JsonOptions);
    }

    private readonly record struct TeamsGraphTarget(string TeamId, string ChannelId);

    private sealed record GraphTokenResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("access_token")] string? AccessToken,
        [property: System.Text.Json.Serialization.JsonPropertyName("refresh_token")] string? RefreshToken);
}
