using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Azure.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace NewsAnalysisAgent.Agents.Infrastructure;

public sealed class FoundryAssistantsClient(
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory,
    TokenCredential credential,
    ILogger<FoundryAssistantsClient> logger,
    MockFoundryAgentClient fallback) : IFoundryAgentClient
{
    private const string DefaultOpenAiApiVersion = "2025-04-01-preview";
    private const string DefaultProjectApiVersion = "2025-05-01";
    private const string CognitiveServicesScope = "https://cognitiveservices.azure.com/.default";
    private const string AiServicesScope = "https://ai.azure.com/.default";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim _assistantLock = new(1, 1);
    private readonly ConcurrentDictionary<string, string> _fileNameCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<AssistantKind, string> _assistantIds = new();

    public async Task<string> InvokeAsync(
        string instructions,
        string userMessage,
        IReadOnlyList<object> tools,
        CancellationToken ct = default)
    {
        var projectEndpoint = configuration["Foundry:ProjectEndpoint"];
        var deployment = configuration["Foundry:DefaultModelDeployment"];
        if (string.IsNullOrWhiteSpace(projectEndpoint) ||
            string.IsNullOrWhiteSpace(deployment))
        {
            return await fallback.InvokeAsync(instructions, userMessage, tools, ct);
        }

        try
        {
            var kind = ResolveAssistantKind(instructions);
            var vectorStoreId = configuration["Foundry:FileSearchVectorStoreId"];
            if (RequiresFileSearch(kind) && string.IsNullOrWhiteSpace(vectorStoreId))
            {
                logger.LogWarning("Foundry:FileSearchVectorStoreId is required for assistant kind {AssistantKind}. Falling back to mock client.", kind);
                return await fallback.InvokeAsync(instructions, userMessage, tools, ct);
            }

            var api = ResolveApi(projectEndpoint);
            var foundryAgentId = ResolveFoundryAgentId(kind);
            var assistantId = await EnsureAssistantAsync(kind, instructions, api, deployment, vectorStoreId, ct);
            return await RunAssistantAsync(assistantId, foundryAgentId, deployment, instructions, userMessage, api, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Foundry Assistants file_search invocation failed. Falling back to mock client.");
            return await fallback.InvokeAsync(instructions, userMessage, tools, ct);
        }
    }

    private async Task<string> EnsureAssistantAsync(
        AssistantKind kind,
        string instructions,
        FoundryAssistantsApi api,
        string deployment,
        string? vectorStoreId,
        CancellationToken ct)
    {
        var configuredId = configuration[$"Foundry:Assistant:{AssistantIdKey(kind)}"];
        if (!string.IsNullOrWhiteSpace(configuredId))
        {
            return configuredId;
        }

        await _assistantLock.WaitAsync(ct);
        try
        {
            if (_assistantIds.TryGetValue(kind, out var cached) && !string.IsNullOrWhiteSpace(cached))
            {
                return cached;
            }

            var name = configuration[$"Foundry:Assistant:{AssistantNameKey(kind)}"] ?? DefaultAssistantName(kind);
            var found = await FindAssistantByNameAsync(api, name, ct);
            var assistantId = found ?? await CreateAssistantAsync(api, deployment, vectorStoreId, name, instructions, ct);
            if (found is not null)
            {
                await UpdateAssistantAsync(api, assistantId, deployment, vectorStoreId, instructions, ct);
            }

            _assistantIds[kind] = assistantId;

            logger.LogInformation("Using Foundry Assistant {AssistantId} ({AssistantName}) for {AssistantKind}.", assistantId, name, kind);
            return assistantId;
        }
        finally
        {
            _assistantLock.Release();
        }
    }

    private async Task<string> RunAssistantAsync(
        string assistantId,
        string foundryAgentId,
        string model,
        string instructions,
        string userMessage,
        FoundryAssistantsApi api,
        CancellationToken ct)
    {
        const int maxRetries = 3;
        var maxWait = TimeSpan.FromSeconds(configuration.GetValue("Foundry:Assistant:RunMaxWaitSeconds", 30));

        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            var thread = await SendJsonAsync(HttpMethod.Post, BuildEndpoint(api, "/threads"), new { }, api, ct);
            var threadId = GetRequiredString(thread, "id");
            await SendJsonAsync(
                HttpMethod.Post,
                BuildEndpoint(api, $"/threads/{Uri.EscapeDataString(threadId)}/messages"),
                new { role = "user", content = userMessage },
                api,
                ct);

            var run = await SendJsonAsync(
                HttpMethod.Post,
                BuildEndpoint(api, $"/threads/{Uri.EscapeDataString(threadId)}/runs"),
                new
                {
                    assistant_id = assistantId,
                    instructions,
                    tool_choice = "auto",
                    temperature = 1
                },
                api,
                ct);
            var runId = GetRequiredString(run, "id");
            var latestRun = run;
            var status = run.TryGetProperty("status", out var initialStatus) ? initialStatus.GetString() : "queued";
            var stopwatch = Stopwatch.StartNew();

            while (status is "queued" or "in_progress" or "requires_action" or "cancelling")
            {
                if (stopwatch.Elapsed > maxWait)
                {
                    throw new TimeoutException($"Foundry Assistant run {runId} did not complete within {maxWait.TotalSeconds:0}s.");
                }

                await Task.Delay(TimeSpan.FromSeconds(1), ct);
                latestRun = await SendAsync(
                    HttpMethod.Get,
                    BuildEndpoint(api, $"/threads/{Uri.EscapeDataString(threadId)}/runs/{Uri.EscapeDataString(runId)}"),
                    content: null,
                    api,
                    ct);
                status = latestRun.TryGetProperty("status", out var statusElement) ? statusElement.GetString() : null;
            }

            if (status == "completed")
            {
                var fileSearchUsed = await TryLogFileSearchAsync(api, threadId, runId, ct);
                var messages = await SendAsync(
                    HttpMethod.Get,
                    BuildEndpoint(api, $"/threads/{Uri.EscapeDataString(threadId)}/messages?limit=10&order=desc", hasQuery: true),
                    content: null,
                    api,
                    ct);
                var message = await ExtractAssistantMessageAsync(messages, api, ct);
                if (string.IsNullOrWhiteSpace(message.Content))
                {
                    throw new InvalidOperationException($"Foundry Assistant run {runId} completed without assistant content.");
                }

                var (inputTokens, outputTokens) = ExtractUsageTokens(latestRun);
                logger.LogInformation(
                    "Foundry Assistant run {RunId} completed. AssistantId={AssistantId}; FoundryAgentId={FoundryAgentId}; file_search_used={FileSearchUsed}; sources={Sources}; input_tokens={InputTokens}; output_tokens={OutputTokens}.",
                    runId,
                    assistantId,
                    foundryAgentId,
                    fileSearchUsed,
                    string.Join(",", message.SourceFileNames),
                    inputTokens,
                    outputTokens);
                GenAITelemetry.RecordChat(
                    agentName: foundryAgentId,
                    model: model,
                    assistantId: foundryAgentId,
                    runId: runId,
                    instructions: instructions,
                    userMessage: userMessage,
                    assistantContent: message.Content,
                    inputTokens: inputTokens,
                    outputTokens: outputTokens);
                return MergeDataReferences(message.Content, message.SourceFileNames);
            }

            // Rate limit or transient failure — retry after backoff
            var errorCode = latestRun.TryGetProperty("last_error", out var lastErr)
                && lastErr.TryGetProperty("code", out var code)
                ? code.GetString() : null;

            if (attempt < maxRetries - 1 && errorCode is "rate_limit_exceeded" or "server_error")
            {
                var backoff = TimeSpan.FromSeconds(Math.Pow(2, attempt + 1) * 5); // 10s, 20s
                logger.LogWarning(
                    "Foundry Assistant run {RunId} failed with {ErrorCode}. Retrying in {Backoff}s (attempt {Attempt}/{MaxRetries}).",
                    runId, errorCode, backoff.TotalSeconds, attempt + 1, maxRetries);
                await Task.Delay(backoff, ct);
                continue;
            }

            throw new InvalidOperationException($"Foundry Assistant run {runId} ended with status {status ?? "(unknown)"}.");
        }

        throw new InvalidOperationException("Foundry Assistant run exhausted all retries.");
    }

    private string ResolveAssistantName(AssistantKind kind) =>
        configuration[$"Foundry:Assistant:{AssistantNameKey(kind)}"] ?? DefaultAssistantName(kind);

    private string ResolveFoundryAgentId(AssistantKind kind) =>
        configuration[$"Foundry:Operate:{kind}AgentId"] ?? DefaultFoundryAgentId(kind);

    private static (long? Input, long? Output) ExtractUsageTokens(JsonElement run)
    {
        if (!run.TryGetProperty("usage", out var usage) || usage.ValueKind != JsonValueKind.Object)
        {
            return (null, null);
        }
        long? input = usage.TryGetProperty("prompt_tokens", out var pt) && pt.ValueKind == JsonValueKind.Number ? pt.GetInt64() : null;
        long? output = usage.TryGetProperty("completion_tokens", out var ct) && ct.ValueKind == JsonValueKind.Number ? ct.GetInt64() : null;
        return (input, output);
    }

    private static string DefaultFoundryAgentId(AssistantKind kind) => kind switch
    {
        AssistantKind.WebResearch => "web-ag:1",
        AssistantKind.Impact => "impact-ag:2",
        AssistantKind.MobileRecommend => "sub-mobile-ag:2",
        AssistantKind.EcommerceRecommend => "sub-internet-ag:2",
        AssistantKind.FintechRecommend => "sub-fintech-ag:2",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    private async Task<bool> TryLogFileSearchAsync(FoundryAssistantsApi api, string threadId, string runId, CancellationToken ct)
    {
        try
        {
            var steps = await SendAsync(
                HttpMethod.Get,
                BuildEndpoint(api, $"/threads/{Uri.EscapeDataString(threadId)}/runs/{Uri.EscapeDataString(runId)}/steps?limit=20", hasQuery: true),
                content: null,
                api,
                ct);
            return ContainsToolCall(steps, "file_search");
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Could not retrieve Foundry Assistant run steps for file_search verification.");
            return false;
        }
    }

    private async Task<string?> FindAssistantByNameAsync(FoundryAssistantsApi api, string name, CancellationToken ct)
    {
        var list = await SendAsync(
            HttpMethod.Get,
            BuildEndpoint(api, "/assistants?limit=100&order=desc", hasQuery: true),
            content: null,
            api,
            ct);
        if (!list.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var assistant in data.EnumerateArray())
        {
            if (assistant.TryGetProperty("name", out var nameElement) &&
                string.Equals(nameElement.GetString(), name, StringComparison.Ordinal) &&
                assistant.TryGetProperty("id", out var idElement))
            {
                return idElement.GetString();
            }
        }

        return null;
    }

    private async Task<string> CreateAssistantAsync(
        FoundryAssistantsApi api,
        string deployment,
        string? vectorStoreId,
        string name,
        string instructions,
        CancellationToken ct)
    {
        var assistant = await SendJsonAsync(
            HttpMethod.Post,
            BuildEndpoint(api, "/assistants"),
            AssistantPayload(deployment, vectorStoreId, name, instructions),
            api,
            ct);
        return GetRequiredString(assistant, "id");
    }

    private async Task UpdateAssistantAsync(
        FoundryAssistantsApi api,
        string assistantId,
        string deployment,
        string? vectorStoreId,
        string instructions,
        CancellationToken ct)
    {
        try
        {
            await SendJsonAsync(
                HttpMethod.Post,
                BuildEndpoint(api, $"/assistants/{Uri.EscapeDataString(assistantId)}"),
                AssistantPayload(deployment, vectorStoreId, name: null, instructions),
                api,
                ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Foundry Assistant {AssistantId} update failed; continuing with existing assistant.", assistantId);
        }
    }

    private static object AssistantPayload(string deployment, string? vectorStoreId, string? name, string instructions)
    {
        var payload = new Dictionary<string, object?>
        {
            ["model"] = deployment,
            ["instructions"] = instructions,
            ["temperature"] = 0.3
        };

        if (!string.IsNullOrWhiteSpace(vectorStoreId))
        {
            payload["tools"] = new object[] { new { type = "file_search" } };
            payload["tool_resources"] = new
            {
                file_search = new
                {
                    vector_store_ids = new[] { vectorStoreId }
                }
            };
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            payload["name"] = name;
        }

        return payload;
    }

    private async Task<JsonElement> SendJsonAsync(HttpMethod method, Uri uri, object payload, FoundryAssistantsApi api, CancellationToken ct) =>
        await SendAsync(method, uri, JsonContent(payload), api, ct);

    private async Task<JsonElement> SendAsync(HttpMethod method, Uri uri, HttpContent? content, FoundryAssistantsApi api, CancellationToken ct)
    {
        var token = await credential.GetTokenAsync(new TokenRequestContext([api.Scope]), ct);
        using var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        request.Content = content;

        var client = httpClientFactory.CreateClient("foundry-agent");
        using var response = await client.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Foundry Assistants API returned {(int)response.StatusCode} {response.ReasonPhrase}: {Truncate(body, 1000)}");
        }

        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }

    private async Task<AssistantMessage> ExtractAssistantMessageAsync(JsonElement messages, FoundryAssistantsApi api, CancellationToken ct)
    {
        if (!messages.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return new AssistantMessage(string.Empty, []);
        }

        foreach (var message in data.EnumerateArray())
        {
            if (!message.TryGetProperty("role", out var role) ||
                !string.Equals(role.GetString(), "assistant", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var textParts = new List<string>();
            var fileIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (message.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in content.EnumerateArray())
                {
                    if (item.TryGetProperty("text", out var text))
                    {
                        if (text.TryGetProperty("value", out var value))
                        {
                            textParts.Add(value.GetString() ?? string.Empty);
                        }

                        if (text.TryGetProperty("annotations", out var annotations) && annotations.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var annotation in annotations.EnumerateArray())
                            {
                                CollectFileIds(annotation, fileIds);
                            }
                        }
                    }
                }
            }

            var fileNames = new List<string>();
            foreach (var fileId in fileIds)
            {
                fileNames.Add(await GetFileNameAsync(fileId, api, ct));
            }

            return new AssistantMessage(string.Join('\n', textParts.Where(static part => !string.IsNullOrWhiteSpace(part))), fileNames);
        }

        return new AssistantMessage(string.Empty, []);
    }

    private async Task<string> GetFileNameAsync(string fileId, FoundryAssistantsApi api, CancellationToken ct)
    {
        if (_fileNameCache.TryGetValue(fileId, out var cached))
        {
            return cached;
        }

        try
        {
            var file = await SendAsync(
                HttpMethod.Get,
                BuildEndpoint(api, $"/files/{Uri.EscapeDataString(fileId)}"),
                content: null,
                api,
                ct);
            var name = file.TryGetProperty("filename", out var filename) && !string.IsNullOrWhiteSpace(filename.GetString())
                ? filename.GetString()!
                : fileId;
            _fileNameCache[fileId] = name;
            return name;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Could not resolve Foundry file name for {FileId}.", fileId);
            return fileId;
        }
    }

    private static void CollectFileIds(JsonElement element, ISet<string> fileIds)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var propertyName in new[] { "file_citation", "file_path" })
        {
            if (element.TryGetProperty(propertyName, out var citation) &&
                citation.TryGetProperty("file_id", out var fileId) &&
                !string.IsNullOrWhiteSpace(fileId.GetString()))
            {
                fileIds.Add(fileId.GetString()!);
            }
        }
    }

    private static string MergeDataReferences(string content, IReadOnlyCollection<string> references)
    {
        if (references.Count == 0)
        {
            return content;
        }

        var jsonText = ExtractJsonObjectOrNull(content);
        if (jsonText is null)
        {
            return content;
        }

        try
        {
            var node = JsonNode.Parse(jsonText) as JsonObject;
            if (node is null)
            {
                return content;
            }

            var existingArray = node["data_references"] as JsonArray;
            var array = existingArray ?? [];
            var existing = array
                .Select(static item => item?.GetValue<string>())
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var reference in references.Where(static value => !string.IsNullOrWhiteSpace(value)))
            {
                if (existing.Add(reference))
                {
                    array.Add(reference);
                }
            }

            if (existingArray is null)
            {
                node["data_references"] = array;
            }

            return node.ToJsonString(JsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return content;
        }
    }

    private static string? ExtractJsonObjectOrNull(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start >= 0 && end >= start ? text[start..(end + 1)] : null;
    }

    private static bool ContainsToolCall(JsonElement element, string toolType)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("type", out var type) &&
                string.Equals(type.GetString(), toolType, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            foreach (var property in element.EnumerateObject())
            {
                if (ContainsToolCall(property.Value, toolType))
                {
                    return true;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (ContainsToolCall(item, toolType))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static AssistantKind ResolveAssistantKind(string instructions)
    {
        if (instructions.Contains("ニュース分析の調査担当エージェント", StringComparison.Ordinal))
        {
            return AssistantKind.WebResearch;
        }

        if (instructions.Contains("対象事業部: モバイル通信", StringComparison.Ordinal))
        {
            return AssistantKind.MobileRecommend;
        }

        if (instructions.Contains("対象事業部: Eコマース", StringComparison.Ordinal))
        {
            return AssistantKind.EcommerceRecommend;
        }

        if (instructions.Contains("対象事業部: Fintech", StringComparison.Ordinal))
        {
            return AssistantKind.FintechRecommend;
        }

        return AssistantKind.Impact;
    }

    private static bool RequiresFileSearch(AssistantKind kind) =>
        kind is AssistantKind.Impact or AssistantKind.MobileRecommend or AssistantKind.EcommerceRecommend or AssistantKind.FintechRecommend;

    private static string AssistantIdKey(AssistantKind kind) => kind switch
    {
        AssistantKind.WebResearch => "WebResearchAssistantId",
        AssistantKind.Impact => "ImpactAssistantId",
        AssistantKind.MobileRecommend => "MobileRecommendAssistantId",
        AssistantKind.EcommerceRecommend => "EcommerceRecommendAssistantId",
        AssistantKind.FintechRecommend => "FintechRecommendAssistantId",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    private static string AssistantNameKey(AssistantKind kind) => kind switch
    {
        AssistantKind.WebResearch => "WebResearchName",
        AssistantKind.Impact => "ImpactName",
        AssistantKind.MobileRecommend => "MobileRecommendName",
        AssistantKind.EcommerceRecommend => "EcommerceRecommendName",
        AssistantKind.FintechRecommend => "FintechRecommendName",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    private static string DefaultAssistantName(AssistantKind kind) => kind switch
    {
        AssistantKind.WebResearch => "nexus6-web-research",
        AssistantKind.Impact => "nexus6-business-impact-filesearch",
        AssistantKind.MobileRecommend => "nexus6-mobile-recommend-filesearch",
        AssistantKind.EcommerceRecommend => "nexus6-ecommerce-recommend-filesearch",
        AssistantKind.FintechRecommend => "nexus6-fintech-recommend-filesearch",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    private static StringContent JsonContent(object value) =>
        new(JsonSerializer.Serialize(value, JsonOptions), Encoding.UTF8, "application/json");

    private FoundryAssistantsApi ResolveApi(string projectEndpoint)
    {
        var useProjectEndpoint = configuration.GetValue("Foundry:Assistant:UseProjectEndpoint", projectEndpoint.Contains("/api/projects/", StringComparison.OrdinalIgnoreCase));
        var baseEndpoint = projectEndpoint.TrimEnd('/');
        var projectSegment = baseEndpoint.IndexOf("/api/projects/", StringComparison.OrdinalIgnoreCase);
        if (!useProjectEndpoint && projectSegment >= 0)
        {
            baseEndpoint = baseEndpoint[..projectSegment];
        }

        var configuredVersion = configuration["Foundry:AssistantsApiVersion"] ?? configuration["Foundry:Assistant:ApiVersion"];
        var apiVersion = configuredVersion ?? (useProjectEndpoint ? DefaultProjectApiVersion : DefaultOpenAiApiVersion);
        var scope = configuration["Foundry:Assistant:Scope"] ?? (useProjectEndpoint ? AiServicesScope : CognitiveServicesScope);
        return new FoundryAssistantsApi(baseEndpoint, useProjectEndpoint ? string.Empty : "/openai", apiVersion, scope);
    }

    private static Uri BuildEndpoint(FoundryAssistantsApi api, string pathAndQuery, bool hasQuery = false)
    {
        var separator = hasQuery ? "&" : "?";
        return new Uri($"{api.BaseEndpoint}{api.PathPrefix}{pathAndQuery}{separator}api-version={Uri.EscapeDataString(api.ApiVersion)}");
    }

    private static string GetRequiredString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var value) && !string.IsNullOrWhiteSpace(value.GetString()))
        {
            return value.GetString()!;
        }

        throw new JsonException($"Foundry Assistants API response did not include required property '{propertyName}'.");
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private enum AssistantKind
    {
        WebResearch,
        Impact,
        MobileRecommend,
        EcommerceRecommend,
        FintechRecommend
    }

    private sealed record AssistantMessage(string Content, IReadOnlyList<string> SourceFileNames);

    private sealed record FoundryAssistantsApi(string BaseEndpoint, string PathPrefix, string ApiVersion, string Scope);
}
