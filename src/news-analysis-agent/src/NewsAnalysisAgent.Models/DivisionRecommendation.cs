namespace NewsAnalysisAgent.Models;

public sealed record DivisionRecommendation(
    DivisionKind Division,
    string Headline,
    string[] NextActions,
    string[] DataReferences);
