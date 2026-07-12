using System.Text.Json.Serialization;

namespace EnvironmentSetup.App.Models;

/// <summary>
/// CLI全体の状態を保持するモデル。setup-state.json に永続化される。
/// </summary>
public class SetupState
{
    [JsonPropertyName("currentStep")]
    public int CurrentStep { get; set; } = 1;

    [JsonPropertyName("completedSteps")]
    public List<int> CompletedSteps { get; set; } = [];

    [JsonPropertyName("assessment")]
    public AssessmentInput? Assessment { get; set; }

    [JsonPropertyName("analysis")]
    public AnalysisResult? Analysis { get; set; }

    [JsonPropertyName("report")]
    public ReportOutput? Report { get; set; }

    [JsonPropertyName("azure")]
    public AzureConfig? Azure { get; set; }

    [JsonPropertyName("deployment")]
    public DeploymentResult? Deployment { get; set; }

    [JsonPropertyName("medallion")]
    public MedallionResult? Medallion { get; set; }

    [JsonPropertyName("ontology")]
    public OntologyResult? Ontology { get; set; }

    /// <summary>
    /// 手動対応が必要な項目リスト (最終サマリーで表示)
    /// </summary>
    [JsonPropertyName("manualActions")]
    public List<ManualAction> ManualActions { get; set; } = [];
}

/// <summary>
/// 手動対応が必要な項目
/// </summary>
public class ManualAction
{
    [JsonPropertyName("step")]
    public int Step { get; set; }

    [JsonPropertyName("target")]
    public string Target { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("details")]
    public List<string> Details { get; set; } = [];
}

/// <summary>
/// Azure サブスクリプション情報
/// </summary>
public class AzureConfig
{
    [JsonPropertyName("subscriptionId")]
    public string SubscriptionId { get; set; } = string.Empty;

    [JsonPropertyName("tenantId")]
    public string TenantId { get; set; } = string.Empty;

    [JsonPropertyName("resourceGroup")]
    public string ResourceGroup { get; set; } = string.Empty;

    [JsonPropertyName("region")]
    public string Region { get; set; } = "swedencentral";

    [JsonPropertyName("userUpn")]
    public string UserUpn { get; set; } = string.Empty;

    [JsonPropertyName("fabricAvailable")]
    public bool FabricAvailable { get; set; }

    [JsonPropertyName("functionsAvailable")]
    public bool FunctionsAvailable { get; set; } = true;
}

/// <summary>
/// デプロイ結果
/// </summary>
public class DeploymentResult
{
    [JsonPropertyName("storageAccountSkills")]
    public string StorageAccountSkills { get; set; } = string.Empty;

    [JsonPropertyName("storageAccountPortal")]
    public string StorageAccountPortal { get; set; } = string.Empty;

    [JsonPropertyName("foundryEndpoint")]
    public string FoundryEndpoint { get; set; } = string.Empty;

    [JsonPropertyName("foundryProjectEndpoint")]
    public string FoundryProjectEndpoint { get; set; } = string.Empty;

    [JsonPropertyName("fabricSqlEndpoint")]
    public string FabricSqlEndpoint { get; set; } = string.Empty;

    [JsonPropertyName("fabricDatabase")]
    public string FabricDatabase { get; set; } = "lh_bronze";

    [JsonPropertyName("containerAppUrl")]
    public string ContainerAppUrl { get; set; } = string.Empty;

    [JsonPropertyName("containerAppNameAgent")]
    public string ContainerAppNameAgent { get; set; } = "ca-nexus6-hosted-agent";

    [JsonPropertyName("containerAppNameWebapp")]
    public string ContainerAppNameWebapp { get; set; } = "ca-nexus6-webapp";

    [JsonPropertyName("acrLoginServer")]
    public string AcrLoginServer { get; set; } = string.Empty;

    [JsonPropertyName("functionAppName")]
    public string FunctionAppName { get; set; } = string.Empty;

    [JsonPropertyName("keyVaultUri")]
    public string KeyVaultUri { get; set; } = string.Empty;

    [JsonPropertyName("appInsightsConnectionString")]
    public string AppInsightsConnectionString { get; set; } = string.Empty;

    [JsonPropertyName("portalBaseUrl")]
    public string PortalBaseUrl { get; set; } = string.Empty;

    [JsonPropertyName("entraAppId")]
    public string EntraAppId { get; set; } = string.Empty;

    [JsonPropertyName("entraAppTenantId")]
    public string EntraAppTenantId { get; set; } = string.Empty;

    [JsonPropertyName("webIqBaseUrl")]
    public string WebIqBaseUrl { get; set; } = string.Empty;

    [JsonPropertyName("foundryVectorStoreId")]
    public string FoundryVectorStoreId { get; set; } = string.Empty;

    [JsonPropertyName("foundryAssistantIds")]
    public Dictionary<string, string> FoundryAssistantIds { get; set; } = [];
}
