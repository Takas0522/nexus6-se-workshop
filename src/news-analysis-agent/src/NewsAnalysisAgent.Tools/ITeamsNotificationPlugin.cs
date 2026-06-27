namespace NewsAnalysisAgent.Tools;

public interface ITeamsNotificationPlugin
{
    Task<string> SendAsync(string division, string cardPayloadJson, CancellationToken ct = default);
}
