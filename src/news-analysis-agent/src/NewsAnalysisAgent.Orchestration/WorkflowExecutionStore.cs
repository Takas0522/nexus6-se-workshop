using NewsAnalysisAgent.Models;

namespace NewsAnalysisAgent.Orchestration;

public sealed class WorkflowExecutionStore
{
    private readonly object _gate = new();

    public WorkflowRunLog? LatestRun { get; private set; }
    public NewsAnalysisContext? LatestContext { get; private set; }

    public void Record(WorkflowRunLog runLog, NewsAnalysisContext context)
    {
        lock (_gate)
        {
            LatestRun = runLog;
            LatestContext = context;
        }
    }
}
