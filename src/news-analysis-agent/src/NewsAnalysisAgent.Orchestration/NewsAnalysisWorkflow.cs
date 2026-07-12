using System.Diagnostics;
using Microsoft.Extensions.Logging;
using NewsAnalysisAgent.Agents;
using NewsAnalysisAgent.Models;
using Polly;
using Polly.Registry;

namespace NewsAnalysisAgent.Orchestration;

public sealed class NewsAnalysisWorkflow(
    IWorkflowStep<NewsAnalysisContext> webResearchAgent,
    IWorkflowStep<NewsAnalysisContext> businessImpactAgent,
    IReadOnlyList<(string Name, IWorkflowStep<NewsAnalysisContext> Step)> divisionRecommendSteps,
    IWorkflowStep<NewsAnalysisContext> notificationAgent,
    WorkflowExecutionStore executionStore,
    ILogger<NewsAnalysisWorkflow> logger,
    ResiliencePipelineProvider<string>? resiliencePipelineProvider = null) : IWorkflow<NewsAnalysisContext>
{
    public async Task<NewsAnalysisContext> RunAsync(NewsAnalysisContext context, CancellationToken ct = default)
    {
        var runId = Guid.NewGuid();
        var startedAt = DateTimeOffset.UtcNow;
        var stepLogs = new List<WorkflowStepLog>();

        await ExecuteStepAsync("web-research", webResearchAgent, context, stepLogs, ct);
        await ExecuteStepAsync("business-impact", businessImpactAgent, context, stepLogs, ct);

        var fanOutTasks = divisionRecommendSteps.Select(item =>
            ExecuteStepAsync($"division-recommend-{item.Name}", item.Step, context, stepLogs, ct));
        await Task.WhenAll(fanOutTasks);

        await ExecuteStepAsync("notification", notificationAgent, context, stepLogs, ct);

        context.CompletedAt = DateTimeOffset.UtcNow;
        var runLog = new WorkflowRunLog(runId, context.OriginalNewsText, startedAt, context.CompletedAt, stepLogs.ToArray());
        executionStore.Record(runLog, context);
        return context;
    }

    private async Task ExecuteStepAsync(
        string stepName,
        IWorkflowStep<NewsAnalysisContext> step,
        NewsAnalysisContext context,
        List<WorkflowStepLog> stepLogs,
        CancellationToken ct)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        string? errorMessage = null;
        var succeeded = false;

        try
        {
            logger.LogInformation("Workflow step {StepName} started", stepName);
            var pipeline = resiliencePipelineProvider?.GetPipeline("agent-retry");
            if (pipeline is null)
            {
                await step.RunAsync(context, ct);
            }
            else
            {
                await pipeline.ExecuteAsync(async token => await step.RunAsync(context, token), ct);
            }

            succeeded = true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            logger.LogError(ex, "Workflow step {StepName} failed; continuing with partial context", stepName);
        }
        finally
        {
            stopwatch.Stop();
            var completedAt = DateTimeOffset.UtcNow;
            lock (stepLogs)
            {
                stepLogs.Add(new WorkflowStepLog(
                    stepName,
                    succeeded,
                    stopwatch.ElapsedMilliseconds,
                    errorMessage,
                    startedAt,
                    completedAt));
            }

            logger.LogInformation(
                "Workflow step {StepName} completed in {DurationMilliseconds} ms. Succeeded: {Succeeded}",
                stepName,
                stopwatch.ElapsedMilliseconds,
                succeeded);
        }
    }
}
