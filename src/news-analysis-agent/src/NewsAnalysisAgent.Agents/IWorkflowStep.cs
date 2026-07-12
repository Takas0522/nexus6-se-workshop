namespace NewsAnalysisAgent.Agents;

public interface IWorkflowStep<TContext>
{
    Task RunAsync(TContext ctx, CancellationToken ct);
}
