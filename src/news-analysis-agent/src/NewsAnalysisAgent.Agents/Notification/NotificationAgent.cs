using System.Text.Json;
using Microsoft.Extensions.Logging;
using NewsAnalysisAgent.Models;
using NewsAnalysisAgent.Tools;

namespace NewsAnalysisAgent.Agents.Notification;

public sealed class NotificationAgent(
    ITeamsNotificationPlugin teamsNotificationPlugin,
    ILogger<NotificationAgent> logger) : IWorkflowStep<NewsAnalysisContext>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = false };

    public async Task RunAsync(NewsAnalysisContext ctx, CancellationToken ct)
    {
        var notifications = ctx.Recommendations
            .OrderBy(recommendation => recommendation.Division)
            .Select(recommendation => new NotificationPayload(
                recommendation.Division.ToString(),
                AdaptiveCardTemplates.Build(recommendation, ResolveRiskLevel(ctx, recommendation.Division))))
            .ToArray();

        logger.LogInformation("NotificationAgent sending {NotificationCount} Teams notifications", notifications.Length);

        var sendTasks = notifications.Select(item => SendOneAsync(item, ct)).ToArray();
        var sendResults = await Task.WhenAll(sendTasks);
        var channels = sendResults.Where(result => result.Channel is not null).Select(result => result.Channel!).ToArray();

        ctx.NotificationResult = new NotificationResult(
            Sent: channels.Length > 0,
            Channels: channels,
            PayloadJson: JsonSerializer.Serialize(new
            {
                ctx.OriginalNewsText,
                Notifications = notifications.Select(item => new
                {
                    item.Division,
                    Card = JsonSerializer.Deserialize<JsonElement>(item.CardPayloadJson)
                }),
                Channels = channels,
                FailedDivisions = sendResults.Where(result => result.Channel is null).Select(result => result.Division)
            }, JsonOptions));
    }

    private async Task<SendResult> SendOneAsync(NotificationPayload payload, CancellationToken ct)
    {
        try
        {
            var channel = await teamsNotificationPlugin.SendAsync(payload.Division, payload.CardPayloadJson, ct);
            return new SendResult(payload.Division, channel);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Teams notification failed for {Division}; continuing with other divisions", payload.Division);
            return new SendResult(payload.Division, null);
        }
    }

    private static string ResolveRiskLevel(NewsAnalysisContext ctx, DivisionKind division) =>
        ctx.ImpactResult?.ImpactScores.FirstOrDefault(score => score.Division == division)?.RiskLevel ?? "unknown";

    private sealed record NotificationPayload(string Division, string CardPayloadJson);

    private sealed record SendResult(string Division, string? Channel);
}
