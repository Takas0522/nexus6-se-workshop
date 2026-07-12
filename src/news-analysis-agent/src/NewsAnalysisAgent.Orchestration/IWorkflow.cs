namespace NewsAnalysisAgent.Orchestration;

public interface IWorkflow<TContext>
{
    Task<TContext> RunAsync(TContext context, CancellationToken ct = default);
}
