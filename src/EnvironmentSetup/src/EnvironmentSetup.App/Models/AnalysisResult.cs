using System.Text.Json.Serialization;

namespace EnvironmentSetup.App.Models;

/// <summary>
/// ステップ2: 分析結果
/// </summary>
public class AnalysisResult
{
    [JsonPropertyName("tables")]
    public List<TableDefinition> Tables { get; set; } = [];

    [JsonPropertyName("estimatedDataCounts")]
    public DataCountEstimate EstimatedDataCounts { get; set; } = new();

    [JsonPropertyName("newsScenarios")]
    public List<NewsScenario> NewsScenarios { get; set; } = [];

    [JsonPropertyName("notificationTemplates")]
    public List<NotificationTemplate> NotificationTemplates { get; set; } = [];
}

public class TableDefinition
{
    [JsonPropertyName("database")]
    public string Database { get; set; } = string.Empty;

    [JsonPropertyName("tableName")]
    public string TableName { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("estimatedRows")]
    public long EstimatedRows { get; set; }

    [JsonPropertyName("columns")]
    public List<ColumnDefinition> Columns { get; set; } = [];
}

public class ColumnDefinition
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("nullable")]
    public bool Nullable { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}

public class DataCountEstimate
{
    [JsonPropertyName("totalCustomers")]
    public int TotalCustomers { get; set; }

    [JsonPropertyName("monthlyTransactions")]
    public Dictionary<string, int> MonthlyTransactions { get; set; } = new();

    [JsonPropertyName("totalMonths")]
    public int TotalMonths { get; set; } = 6;

    [JsonPropertyName("totalRecords")]
    public long TotalRecords { get; set; }
}

public class NewsScenario
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("impactDomains")]
    public List<string> ImpactDomains { get; set; } = [];
}

public class NotificationTemplate
{
    [JsonPropertyName("domain")]
    public string Domain { get; set; } = string.Empty;

    [JsonPropertyName("newsScenarioId")]
    public string NewsScenarioId { get; set; } = string.Empty;

    [JsonPropertyName("channel")]
    public string Channel { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("body")]
    public string Body { get; set; } = string.Empty;
}
