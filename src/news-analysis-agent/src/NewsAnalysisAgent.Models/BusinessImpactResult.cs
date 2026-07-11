namespace NewsAnalysisAgent.Models;

public sealed record BusinessImpactResult(
    ImpactScore[] ImpactScores,
    string[] ImpactReasons,
    string[] PriorityOrder)
{
    public string[] DataReferences { get; init; } = [];
    public string[] SourceFiles { get; init; } = [];
    public KpiReference[] KpiReferences { get; init; } = [];
}

public sealed record ImpactScore(
    string Division,
    double Score,
    string RiskLevel);
