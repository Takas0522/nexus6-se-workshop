namespace EnvironmentSetup.App.Models;

/// <summary>
/// 非対話モードで使用するパラメータ群
/// </summary>
public class NonInteractiveConfig
{
    public bool Enabled { get; init; }
    public string[] Domains { get; init; } = [];
    public int Employees { get; init; }
    public string NotificationTeam { get; init; } = "";
    public string[] NotificationChannels { get; init; } = [];
    public string ResourceGroup { get; init; } = "";
    public string WebIqApiKey { get; init; } = "";
}
