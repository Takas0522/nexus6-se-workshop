namespace NewsAnalysisAgent.Models;

public sealed record NotificationResult(
    bool Sent,
    string[] Channels,
    string PayloadJson);
