namespace NewsAnalysisAgent.Models;

public sealed record NewsAnalysisJob(
    string? Url,
    string? NewsText,
    string? OriginalNewsText = null,
    string? ScenarioHint = null,
    string[]? SearchHints = null);
