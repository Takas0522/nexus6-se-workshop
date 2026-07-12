using System.Diagnostics;
using System.Text.Json;

namespace NewsAnalysisAgent.Agents.Infrastructure;

public static class GenAITelemetry
{
    public const string SourceName = "Nexus6.NewsAnalysisAgent.GenAI";

    private static readonly ActivitySource Source = new(SourceName);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void RecordChat(
        string agentName,
        string model,
        string assistantId,
        string? runId,
        string instructions,
        string userMessage,
        string assistantContent,
        long? inputTokens = null,
        long? outputTokens = null)
    {
        using var activity = Source.StartActivity($"chat {model}", ActivityKind.Client);
        if (activity is null)
        {
            return;
        }

        activity.SetTag("gen_ai.system", "az.ai.openai");
        activity.SetTag("gen_ai.operation.name", "chat");
        activity.SetTag("gen_ai.request.model", model);
        activity.SetTag("gen_ai.response.model", model);
        activity.SetTag("gen_ai.agent.name", agentName);
        activity.SetTag("gen_ai.agent.id", assistantId);
        if (!string.IsNullOrWhiteSpace(runId))
        {
            activity.SetTag("gen_ai.response.id", runId);
        }
        if (inputTokens.HasValue)
        {
            activity.SetTag("gen_ai.usage.input_tokens", inputTokens.Value);
        }
        if (outputTokens.HasValue)
        {
            activity.SetTag("gen_ai.usage.output_tokens", outputTokens.Value);
        }

        var inputJson = JsonSerializer.Serialize(new object[]
        {
            new
            {
                role = "developer",
                parts = new object[] { new { type = "text", content = instructions } }
            },
            new
            {
                role = "user",
                parts = new object[] { new { type = "text", content = userMessage } }
            }
        }, JsonOptions);
        var outputJson = JsonSerializer.Serialize(new object[]
        {
            new
            {
                role = "assistant",
                parts = new object[] { new { type = "text", content = assistantContent } },
                finish_reason = "stop"
            }
        }, JsonOptions);
        activity.SetTag("gen_ai.input.messages", inputJson);
        activity.SetTag("gen_ai.output.messages", outputJson);
    }
}
