using Microsoft.Extensions.Logging;
using NewsAnalysisAgent.Agents.Infrastructure;
using NewsAnalysisAgent.Agents.Infrastructure.Knowledge;
using NewsAnalysisAgent.Models;
using NewsAnalysisAgent.Tools;

namespace NewsAnalysisAgent.Agents.DivisionRecommend;

public interface IDivisionRecommendAgentFactory
{
    IWorkflowStep<NewsAnalysisContext> Create(DivisionKind division);
}

public sealed class DivisionRecommendAgentFactory(
    IMobileDataPlugin mobileDataPlugin,
    IEcommerceDataPlugin ecommerceDataPlugin,
    IFintechDataPlugin fintechDataPlugin,
    IFoundryAgentClient foundryAgentClient,
    IKnowledgeProvider knowledgeProvider,
    ILogger<DivisionRecommendAgent> logger) : IDivisionRecommendAgentFactory
{
    public IWorkflowStep<NewsAnalysisContext> Create(DivisionKind division) => division switch
    {
        DivisionKind.Mobile => new DivisionRecommendAgent(division, mobileDataPlugin, foundryAgentClient, knowledgeProvider, logger),
        DivisionKind.Ecommerce => new DivisionRecommendAgent(division, ecommerceDataPlugin, foundryAgentClient, knowledgeProvider, logger),
        DivisionKind.Fintech => new DivisionRecommendAgent(division, fintechDataPlugin, foundryAgentClient, knowledgeProvider, logger),
        _ => throw new ArgumentOutOfRangeException(nameof(division), division, "Unsupported division.")
    };
}
