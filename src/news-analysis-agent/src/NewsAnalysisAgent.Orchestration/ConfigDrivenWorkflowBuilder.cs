using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NewsAnalysisAgent.Agents;
using NewsAnalysisAgent.Agents.BusinessImpact;
using NewsAnalysisAgent.Agents.DivisionRecommend;
using NewsAnalysisAgent.Agents.Notification;
using NewsAnalysisAgent.Agents.WebResearch;
using NewsAnalysisAgent.Models;
using Polly.Registry;

namespace NewsAnalysisAgent.Orchestration;

/// <summary>
/// DivisionsConfig (Blob) ベースで動的に事業部ステップを構築するワークフロービルダー。
/// DivisionsConfig が空でない場合はこちらが使用され、空の場合は従来の固定3事業ビルダーにフォールバックする。
/// </summary>
public sealed class ConfigDrivenWorkflowBuilder
{
    private readonly WebResearchAgent _webResearchAgent;
    private readonly BusinessImpactAgent _businessImpactAgent;
    private readonly IConfigDrivenRecommendAgentFactory _recommendFactory;
    private readonly NotificationAgent _notificationAgent;
    private readonly WorkflowExecutionStore _executionStore;
    private readonly ILogger<NewsAnalysisWorkflow> _workflowLogger;
    private readonly ResiliencePipelineProvider<string>? _resiliencePipelineProvider;
    private readonly DivisionsConfig _config;

    public ConfigDrivenWorkflowBuilder(
        WebResearchAgent webResearchAgent,
        BusinessImpactAgent businessImpactAgent,
        IConfigDrivenRecommendAgentFactory recommendFactory,
        NotificationAgent notificationAgent,
        WorkflowExecutionStore executionStore,
        IOptions<DivisionsConfig> config,
        ILogger<NewsAnalysisWorkflow> workflowLogger,
        ResiliencePipelineProvider<string>? resiliencePipelineProvider = null)
    {
        _webResearchAgent = webResearchAgent;
        _businessImpactAgent = businessImpactAgent;
        _recommendFactory = recommendFactory;
        _notificationAgent = notificationAgent;
        _executionStore = executionStore;
        _config = config.Value;
        _workflowLogger = workflowLogger;
        _resiliencePipelineProvider = resiliencePipelineProvider;
    }

    public IWorkflow<NewsAnalysisContext> Build()
    {
        var divisionSteps = _recommendFactory.CreateAll();

        return new NewsAnalysisWorkflow(
            _webResearchAgent,
            _businessImpactAgent,
            divisionSteps,
            _notificationAgent,
            _executionStore,
            _workflowLogger,
            _resiliencePipelineProvider);
    }

    /// <summary>設定に事業部が定義されているかどうか</summary>
    public bool HasDivisions => _config.Divisions.Count > 0;
}
