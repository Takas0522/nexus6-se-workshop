using System.Text.Json.Serialization;

namespace EnvironmentSetup.App.Models;

/// <summary>
/// オントロジー作成結果
/// </summary>
public class OntologyResult
{
    [JsonPropertyName("ontologyId")]
    public string OntologyId { get; set; } = string.Empty;

    [JsonPropertyName("workspaceId")]
    public string WorkspaceId { get; set; } = string.Empty;

    [JsonPropertyName("lakehouseId")]
    public string LakehouseId { get; set; } = string.Empty;

    [JsonPropertyName("entityTypeCount")]
    public int EntityTypeCount { get; set; }

    [JsonPropertyName("relationshipCount")]
    public int RelationshipCount { get; set; }
}
