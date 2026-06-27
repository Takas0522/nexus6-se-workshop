namespace NewsAnalysisAgent.Models;

public sealed record BusinessImpactResult(
    ImpactScore[] ImpactScores,
    string[] ImpactReasons,
    DivisionKind[] PriorityOrder);

public sealed record ImpactScore(
    DivisionKind Division,
    double Score,
    string RiskLevel);

public enum DivisionKind
{
    Mobile,
    Ecommerce,
    Fintech
}
