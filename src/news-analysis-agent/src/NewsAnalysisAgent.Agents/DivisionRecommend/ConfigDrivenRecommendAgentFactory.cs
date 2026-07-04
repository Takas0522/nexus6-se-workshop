using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NewsAnalysisAgent.Agents.Infrastructure;
using NewsAnalysisAgent.Agents.Infrastructure.Knowledge;
using NewsAnalysisAgent.Models;
using NewsAnalysisAgent.Tools;

namespace NewsAnalysisAgent.Agents.DivisionRecommend;

/// <summary>
/// DivisionsConfig (Blob から読み込み) に基づいて、任意の事業部数の
/// DivisionRecommendAgent ステップを動的に生成するファクトリ。
/// </summary>
public interface IConfigDrivenRecommendAgentFactory
{
    /// <summary>設定に定義された全事業部のステップを生成する</summary>
    IReadOnlyList<(string Name, IWorkflowStep<NewsAnalysisContext> Step)> CreateAll();
}

public sealed class ConfigDrivenRecommendAgentFactory : IConfigDrivenRecommendAgentFactory
{
    private readonly DivisionsConfig _config;
    private readonly IFoundryAgentClient _foundryAgentClient;
    private readonly IKnowledgeProvider _knowledgeProvider;
    private readonly IReadOnlyList<IDivisionDataPlugin> _dataPlugins;
    private readonly ILogger<DivisionRecommendAgent> _logger;

    public ConfigDrivenRecommendAgentFactory(
        IOptions<DivisionsConfig> config,
        IFoundryAgentClient foundryAgentClient,
        IKnowledgeProvider knowledgeProvider,
        IEnumerable<IDivisionDataPlugin> dataPlugins,
        ILogger<DivisionRecommendAgent> logger)
    {
        _config = config.Value;
        _foundryAgentClient = foundryAgentClient;
        _knowledgeProvider = knowledgeProvider;
        _dataPlugins = dataPlugins.ToList();
        _logger = logger;
    }

    public IReadOnlyList<(string Name, IWorkflowStep<NewsAnalysisContext> Step)> CreateAll()
    {
        var steps = new List<(string, IWorkflowStep<NewsAnalysisContext>)>();

        foreach (var division in _config.Divisions)
        {
            var plugin = _dataPlugins.FirstOrDefault(p => p.DivisionId == division.Id);
            var step = new ConfigDrivenDivisionRecommendAgent(
                division,
                plugin,
                _foundryAgentClient,
                _knowledgeProvider,
                _logger);
            steps.Add((division.Id, step));
        }

        return steps;
    }
}
