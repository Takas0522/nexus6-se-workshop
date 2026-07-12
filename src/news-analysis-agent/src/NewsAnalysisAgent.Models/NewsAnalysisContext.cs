using System.Text.Json.Serialization;

namespace NewsAnalysisAgent.Models;

public sealed class NewsAnalysisContext
{
    public string OriginalNewsText { get; init; } = string.Empty;

    [JsonPropertyName("search_hints")]
    public string[] SearchHints { get; init; } = [];

    public WebResearchResult? WebResearchResult { get; set; }
    public BusinessImpactResult? ImpactResult { get; set; }
    public List<DivisionRecommendation> Recommendations { get; set; } = [];
    public NotificationResult? NotificationResult { get; set; }
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
}
