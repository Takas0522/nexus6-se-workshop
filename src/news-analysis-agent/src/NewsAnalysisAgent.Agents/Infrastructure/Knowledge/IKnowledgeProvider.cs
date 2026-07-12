namespace NewsAnalysisAgent.Agents.Infrastructure.Knowledge;

public interface IKnowledgeProvider
{
    Task<IReadOnlyList<KnowledgeSnippet>> SearchAsync(
        string query,
        int maxResults = 6,
        int maxSnippetLength = 1000,
        CancellationToken ct = default);
}

public sealed record KnowledgeSnippet(string Division, string Topic, string Snippet);
