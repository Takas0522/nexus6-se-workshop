using System.Text.Json.Serialization;

namespace EnvironmentSetup.App.Models;

/// <summary>
/// ステップ1: アセスメント入力
/// </summary>
public class AssessmentInput
{
    [JsonPropertyName("domains")]
    public List<string> Domains { get; set; } = ["モバイル通信", "Eコマース", "フィンテック"];

    [JsonPropertyName("employeeCount")]
    public int EmployeeCount { get; set; } = 5000;

    [JsonPropertyName("notificationTeam")]
    public string NotificationTeam { get; set; } = "PartnerIQ-Alerts";

    [JsonPropertyName("notificationChannels")]
    public List<string> NotificationChannels { get; set; } = ["mobile-alerts", "ecommerce-alerts", "fintech-alerts"];
}
