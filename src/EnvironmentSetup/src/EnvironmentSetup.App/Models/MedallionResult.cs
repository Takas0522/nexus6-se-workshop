using System.Text.Json.Serialization;

namespace EnvironmentSetup.App.Models;

/// <summary>
/// メダリオンアーキテクチャ構築結果
/// </summary>
public class MedallionResult
{
    [JsonPropertyName("workspaceId")]
    public string WorkspaceId { get; set; } = string.Empty;

    [JsonPropertyName("bronzeLakehouseId")]
    public string BronzeLakehouseId { get; set; } = string.Empty;

    [JsonPropertyName("silverLakehouseId")]
    public string SilverLakehouseId { get; set; } = string.Empty;

    [JsonPropertyName("goldLakehouseId")]
    public string GoldLakehouseId { get; set; } = string.Empty;

    [JsonPropertyName("bronzeToSilverNotebookId")]
    public string BronzeToSilverNotebookId { get; set; } = string.Empty;

    [JsonPropertyName("silverToGoldNotebookId")]
    public string SilverToGoldNotebookId { get; set; } = string.Empty;
}
