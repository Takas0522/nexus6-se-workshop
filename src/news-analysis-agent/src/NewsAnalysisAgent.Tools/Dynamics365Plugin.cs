using System.Text.Json;
using Microsoft.Extensions.Logging;
using NewsAnalysisAgent.Models;

namespace NewsAnalysisAgent.Tools;

public interface IDynamics365Plugin
{
    Task<string> PublishAsync(DivisionRecommendation recommendation, CancellationToken ct = default);
}

public sealed class Dynamics365Plugin(ILogger<Dynamics365Plugin> logger) : IDynamics365Plugin
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly string LogDirectory = LocalLogPath.For("logs", "dynamics-mock");

    public async Task<string> PublishAsync(DivisionRecommendation recommendation, CancellationToken ct = default)
    {
        Directory.CreateDirectory(LogDirectory);
        var fileName = $"{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{recommendation.Division}-{Guid.NewGuid():N}.json";
        var path = Path.Combine(LogDirectory, fileName);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(recommendation, JsonOptions), ct);
        logger.LogInformation("Mock Dynamics 365 recommendation saved for {Division}: {Path}", recommendation.Division, path);
        return $"mock-dynamics:{recommendation.Division}";
    }

    // Future extension point: replace this mock with Dataverse Web API publishing when Demo constraints are lifted.
}
