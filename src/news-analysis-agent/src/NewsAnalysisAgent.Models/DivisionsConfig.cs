namespace NewsAnalysisAgent.Models;

/// <summary>
/// Blob (domain-config.json) から読み込まれる事業部設定。
/// EnvironmentSetup の Assessment.Domains に基づいて生成される。
/// </summary>
public sealed class DivisionsConfig
{
    public List<DivisionConfig> Divisions { get; set; } = [];
}

/// <summary>
/// 個別事業部の設定
/// </summary>
public sealed class DivisionConfig
{
    /// <summary>事業部識別子 (例: "mobile", "logistics")</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>表示ラベル (例: "モバイル通信")</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>関心領域のリスト (プロンプトに注入される)</summary>
    public List<string> InterestAreas { get; set; } = [];

    /// <summary>Fabric Gold テーブル名 (例: "mobile_ai.risk_summary")</summary>
    public string FabricTable { get; set; } = string.Empty;

    /// <summary>Teams Graph 通知先 Team ID</summary>
    public string TeamsTeamId { get; set; } = string.Empty;

    /// <summary>Teams Graph 通知先 Channel ID</summary>
    public string TeamsChannelId { get; set; } = string.Empty;

    /// <summary>Teams Workflows webhook URL (空なら Graph API を使用)</summary>
    public string TeamsWorkflowsUrl { get; set; } = string.Empty;

    /// <summary>Foundry Assistant 名 (file_search 用)</summary>
    public string FoundryAssistantName { get; set; } = string.Empty;
}
