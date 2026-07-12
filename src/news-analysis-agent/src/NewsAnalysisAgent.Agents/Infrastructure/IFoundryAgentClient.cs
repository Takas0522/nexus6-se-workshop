namespace NewsAnalysisAgent.Agents.Infrastructure;

public interface IFoundryAgentClient
{
    Task<string> InvokeAsync(
        string instructions,
        string userMessage,
        IReadOnlyList<object> tools,
        CancellationToken ct = default);
}
