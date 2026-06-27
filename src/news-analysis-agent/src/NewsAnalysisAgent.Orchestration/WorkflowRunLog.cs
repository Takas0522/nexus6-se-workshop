namespace NewsAnalysisAgent.Orchestration;

public sealed record WorkflowStepLog(
    string StepName,
    bool Succeeded,
    long DurationMilliseconds,
    string? ErrorMessage,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt);

public sealed record WorkflowRunLog(
    Guid RunId,
    string OriginalNewsText,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<WorkflowStepLog> Steps);
