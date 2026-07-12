namespace DemoDataGenerator.Data.Copilot;

public interface ICopilotPromptRunner
{
    Task<string> RunAsync(string prompt, CancellationToken cancellationToken = default);
}

public sealed class TemplateCopilotPromptRunner : ICopilotPromptRunner
{
    public Task<string> RunAsync(string prompt, CancellationToken cancellationToken = default) =>
        Task.FromResult($"template:{prompt.GetHashCode():X}");
}

public sealed class EnvironmentCopilotPromptRunner : ICopilotPromptRunner
{
    private readonly ICopilotPromptRunner fallback = new TemplateCopilotPromptRunner();

    public Task<string> RunAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        return string.IsNullOrWhiteSpace(token)
            ? fallback.RunAsync(prompt, cancellationToken)
            : fallback.RunAsync(prompt, cancellationToken);
    }
}
