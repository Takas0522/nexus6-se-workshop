using System.Text.Json.Serialization;
using NewsAnalysisAgent.Models;

namespace NewsAnalysisWebApp.Api.Models;

public sealed record AnalysisRequest(
    string NewsText,
    string[]? SearchHints = null);

public sealed record AnalysisResponse(
    [property: JsonPropertyName("webResearch")] WebResearchResult? WebResearch,
    [property: JsonPropertyName("businessImpact")] BusinessImpactResult? BusinessImpact,
    [property: JsonPropertyName("recommendations")] List<DivisionRecommendation> Recommendations,
    [property: JsonPropertyName("startedAt")] DateTimeOffset StartedAt,
    [property: JsonPropertyName("completedAt")] DateTimeOffset? CompletedAt);

public sealed record NewsArticle(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("summary")] string Summary);
