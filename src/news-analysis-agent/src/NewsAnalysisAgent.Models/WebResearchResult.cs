using System.Text.Json.Serialization;

namespace NewsAnalysisAgent.Models;

public sealed record WebResearchResult(
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("key_factors")] string[] KeyFactors,
    [property: JsonPropertyName("source_urls")] string[] SourceUrls);
