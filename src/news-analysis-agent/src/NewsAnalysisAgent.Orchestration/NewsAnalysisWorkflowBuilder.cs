using Microsoft.Extensions.Logging;
using Polly.Registry;
using NewsAnalysisAgent.Agents.BusinessImpact;
using NewsAnalysisAgent.Agents.DivisionRecommend;
using NewsAnalysisAgent.Agents.Notification;
using NewsAnalysisAgent.Agents.WebResearch;
using NewsAnalysisAgent.Models;

namespace NewsAnalysisAgent.Orchestration;

public sealed class NewsAnalysisWorkflowBuilder
{
    private readonly WebResearchAgent _webResearchAgent;
    private readonly BusinessImpactAgent _businessImpactAgent;
    private readonly IDivisionRecommendAgentFactory _recommendAgentFactory;
    private readonly NotificationAgent _notificationAgent;
    private readonly WorkflowExecutionStore _executionStore;
    private readonly ILogger<NewsAnalysisWorkflow> _workflowLogger;
    private readonly ResiliencePipelineProvider<string>? _resiliencePipelineProvider;

    public NewsAnalysisWorkflowBuilder(
        WebResearchAgent webResearchAgent,
        BusinessImpactAgent businessImpactAgent,
        IDivisionRecommendAgentFactory recommendAgentFactory,
        NotificationAgent notificationAgent,
        WorkflowExecutionStore executionStore,
        ILogger<NewsAnalysisWorkflow> workflowLogger,
        ResiliencePipelineProvider<string>? resiliencePipelineProvider = null)
    {
        _webResearchAgent = webResearchAgent;
        _businessImpactAgent = businessImpactAgent;
        _recommendAgentFactory = recommendAgentFactory;
        _notificationAgent = notificationAgent;
        _executionStore = executionStore;
        _workflowLogger = workflowLogger;
        _resiliencePipelineProvider = resiliencePipelineProvider;
    }

    public IWorkflow<NewsAnalysisContext> Build()
    {
        // Microsoft Agent Framework Workflow Preview APIs are intentionally isolated here.
        // Re-evaluate Microsoft.Agents.AI.Workflows in tracks F/G/H/I and replace this adapter when the API stabilizes.
        var divisionSteps = new[]
        {
            ("mobile", _recommendAgentFactory.Create(DivisionKind.Mobile)),
            ("ecommerce", _recommendAgentFactory.Create(DivisionKind.Ecommerce)),
            ("fintech", _recommendAgentFactory.Create(DivisionKind.Fintech))
        };

        return new NewsAnalysisWorkflow(
            _webResearchAgent,
            _businessImpactAgent,
            divisionSteps,
            _notificationAgent,
            _executionStore,
            _workflowLogger,
            _resiliencePipelineProvider);
    }
}
