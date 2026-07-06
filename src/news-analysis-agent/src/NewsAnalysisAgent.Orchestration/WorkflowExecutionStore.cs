using NewsAnalysisAgent.Models;
using System.Collections.Concurrent;

namespace NewsAnalysisAgent.Orchestration;

public sealed class WorkflowExecutionStore
{
    private readonly object _gate = new();
    private readonly ConcurrentDictionary<string, AnalysisJob> _jobs = new();

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

    public string CreateJob() 
    {
        var id = Guid.NewGuid().ToString("N")[..12];
        _jobs[id] = new AnalysisJob { Status = "running" };
        return id;
    }

    public void CompleteJob(string id, NewsAnalysisContext context)
    {
        if (_jobs.TryGetValue(id, out var job))
        {
            job.Context = context;
            job.Status = "completed";
        }
    }

    public void FailJob(string id, string error)
    {
        if (_jobs.TryGetValue(id, out var job))
        {
            job.Error = error;
            job.Status = "failed";
        }
    }

    public AnalysisJob? GetJob(string id) => _jobs.GetValueOrDefault(id);
}

public sealed class AnalysisJob
{
    public string Status { get; set; } = "running";
    public NewsAnalysisContext? Context { get; set; }
    public string? Error { get; set; }
}
