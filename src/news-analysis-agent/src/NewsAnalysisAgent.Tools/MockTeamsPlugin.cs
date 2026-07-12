using Microsoft.Extensions.Logging;

namespace NewsAnalysisAgent.Tools;

public sealed class MockTeamsPlugin(ILogger<MockTeamsPlugin> logger) : ITeamsNotificationPlugin
{
    private static readonly string LogDirectory = LocalLogPath.For("logs", "teams-mock");

    public async Task<string> SendAsync(string division, string cardPayloadJson, CancellationToken ct = default)
    {
        Directory.CreateDirectory(LogDirectory);
        var fileName = $"{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{division}-{Guid.NewGuid():N}.json";
        var path = Path.Combine(LogDirectory, fileName);
        await File.WriteAllTextAsync(path, cardPayloadJson, ct);
        logger.LogInformation("Mock Teams notification saved for {Division}: {Path}", division, path);
        return $"mock:{division}";
    }
}
