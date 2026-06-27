using System.Text.Json;

namespace NewsAnalysisAgent.Agents.Infrastructure;

public sealed class MockFoundryAgentClient : IFoundryAgentClient
{
    public Task<string> InvokeAsync(
        string instructions,
        string userMessage,
        IReadOnlyList<object> tools,
        CancellationToken ct = default)
    {
        if (userMessage.Contains("\"target_division\"", StringComparison.OrdinalIgnoreCase))
        {
            var division = ExtractDivision(userMessage);
            var recommendationJson = JsonSerializer.Serialize(new
            {
                division,
                headline = $"{division} mock recommendation based on KPI and Skill/DS.",
                next_actions = new[] { "KPI を再確認", "高優先リスクの対応案を事業部でレビュー" },
                data_references = new[] { $"{division}_ai.risk_summary", "mock-skill-ds" }
            });
            return Task.FromResult(recommendationJson);
        }

        var json = JsonSerializer.Serialize(new
        {
            summary = "Mock web research summary for local execution.",
            key_factors = new[] { "為替・金融市場の変動", "国内競合他社の動向", "業界トレンドの変化" },
            source_urls = new[] { "https://example.com/mock-news", "https://example.com/mock-market" }
        });
        return Task.FromResult(json);
    }

    private static string ExtractDivision(string userMessage)
    {
        try
        {
            using var document = JsonDocument.Parse(userMessage);
            return document.RootElement.TryGetProperty("target_division", out var division)
                ? division.GetString() ?? "unknown"
                : "unknown";
        }
        catch (JsonException)
        {
            return "unknown";
        }
    }
}
