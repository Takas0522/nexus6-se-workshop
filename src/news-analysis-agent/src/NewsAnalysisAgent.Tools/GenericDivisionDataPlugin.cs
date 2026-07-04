using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NewsAnalysisAgent.Models;

namespace NewsAnalysisAgent.Tools;

/// <summary>
/// 設定駆動型の汎用事業部データプラグイン。
/// DivisionConfig.FabricTable を参照して KPI を取得する。
/// 事業部固有の IMobileDataPlugin / IEcommerceDataPlugin / IFintechDataPlugin を統合する。
/// </summary>
public interface IDivisionDataPlugin
{
    string DivisionId { get; }
    Task<string> GetRepresentativeKpisAsync(string scenario, CancellationToken ct = default);
    Task<string> GetDetailedKpisAsync(string scenario, CancellationToken ct = default);
}

public sealed class GenericDivisionDataPlugin : IDivisionDataPlugin
{
    private readonly DivisionConfig _config;
    private readonly IConfiguration _appConfig;
    private readonly ILogger _logger;

    public string DivisionId => _config.Id;

    public GenericDivisionDataPlugin(
        DivisionConfig config,
        IConfiguration appConfig,
        ILogger<GenericDivisionDataPlugin> logger)
    {
        _config = config;
        _appConfig = appConfig;
        _logger = logger;
    }

    public Task<string> GetRepresentativeKpisAsync(string scenario, CancellationToken ct = default) =>
        QueryAsync(scenario, rowLimit: 5, ct);

    public Task<string> GetDetailedKpisAsync(string scenario, CancellationToken ct = default) =>
        QueryAsync(scenario, rowLimit: 20, ct);

    private async Task<string> QueryAsync(string scenario, int rowLimit, CancellationToken ct)
    {
        var connectionString = FabricSql.BuildConnectionString(_appConfig);
        if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(_config.FabricTable))
        {
            return MockResponse(scenario, rowLimit);
        }

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = $"""
                SELECT TOP (@rowLimit) year_month, metric_name, metric_value, metric_unit, description
                FROM {_config.FabricTable}
                ORDER BY year_month DESC, metric_name
                """;
            command.Parameters.AddWithValue("@rowLimit", rowLimit);
            return await FabricSql.ReadRiskSummaryJsonAsync(command, $"fabric-{_config.Id}", _config.FabricTable, scenario, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Fabric KPI query failed for division {Division}. Returning mock.", _config.Id);
            return MockResponse(scenario, rowLimit);
        }
    }

    private string MockResponse(string scenario, int rowLimit)
    {
        return JsonSerializer.Serialize(new
        {
            source = $"mock-{_config.Id}",
            scenario_hash = FabricSql.StableHash(scenario),
            table = _config.FabricTable,
            rows = Array.Empty<object>(),
            note = $"No Fabric connection or table configured for division '{_config.Id}'. Configure FabricTable in domain-config.json."
        });
    }
}
