using System.Text.Json.Serialization;

namespace EnvironmentSetup.App.Models;

/// <summary>
/// ステップ3: レポート出力
/// </summary>
public class ReportOutput
{
    [JsonPropertyName("generatedAt")]
    public DateTimeOffset GeneratedAt { get; set; }

    [JsonPropertyName("approved")]
    public bool Approved { get; set; }

    [JsonPropertyName("outputPath")]
    public string OutputPath { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}
