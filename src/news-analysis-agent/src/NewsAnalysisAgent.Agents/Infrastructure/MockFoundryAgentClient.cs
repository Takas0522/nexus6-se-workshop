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
                next_actions = new[]
                {
                    new { title = "KPI を再確認", body = "参照 KPI と Skill/DS のしきい値を担当者が確認する。" },
                    new { title = "対応案をレビュー", body = "高優先リスクの施策案を事業部でレビューする。" }
                },
                data_references = new[] { $"{division}_ai.risk_summary", $"{division}_skill_mock.md", $"ds_{division}_ai.md" },
                source_files = new[] { $"{division}_skill_mock.md" },
                kpi_references = new[] { new { physical_name = "gross_revenue_jpy", value = "1000000", unit = "JPY", table = $"{division}_ai.risk_summary" } }
            });
            return Task.FromResult(recommendationJson);
        }

        var json = JsonSerializer.Serialize(new
        {
            summary = "Mock web research summary for local execution.",
            key_factors = new[] { "為替・金融市場の変動", "国内競合他社の動向", "業界トレンドの変化" },
            source_urls = Array.Empty<string>()
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
