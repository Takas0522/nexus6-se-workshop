using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace NewsAnalysisAgent.Tools;

public interface IFintechDataPlugin
{
    Task<string> GetRepresentativeKpisAsync(string scenario, CancellationToken ct = default);
    Task<string> GetDetailedKpisAsync(string scenario, CancellationToken ct = default);
}

public sealed class FintechDataPlugin(
    IConfiguration configuration,
    MockFintechDataPlugin fallback,
    ILogger<FintechDataPlugin> logger) : IFintechDataPlugin
{
    public Task<string> GetRepresentativeKpisAsync(string scenario, CancellationToken ct = default) =>
        QueryOrFallbackAsync("fintech_ai.risk_summary", scenario, "representative", fallback.GetRepresentativeKpisAsync, ct);

    public Task<string> GetDetailedKpisAsync(string scenario, CancellationToken ct = default) =>
        QueryOrFallbackAsync("fintech_ai.risk_summary", scenario, "detail", fallback.GetDetailedKpisAsync, ct);

    private async Task<string> QueryOrFallbackAsync(
        string tableName,
        string scenario,
        string detailLevel,
        Func<string, CancellationToken, Task<string>> fallbackQuery,
        CancellationToken ct)
    {
        var connectionString = FabricSql.BuildConnectionString(configuration);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return await fallbackQuery(scenario, ct);
        }

        try
        {
            var rowLimit = detailLevel == "representative" ? 5 : 20;
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = $"""
                SELECT TOP (@rowLimit) year_month, metric_name, metric_value, metric_unit, description
                FROM {tableName}
                ORDER BY year_month DESC, metric_name
                """;
            command.Parameters.AddWithValue("@rowLimit", rowLimit);
            return await FabricSql.ReadRiskSummaryJsonAsync(command, "fabric-fintech", tableName, scenario, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Fabric Fintech KPI query failed. Falling back to mock data.");
            return await fallbackQuery(scenario, ct);
        }
    }
}

public sealed class MockFintechDataPlugin : IFintechDataPlugin
{
    public Task<string> GetRepresentativeKpisAsync(string scenario, CancellationToken ct = default) =>
        Task.FromResult(JsonSerializer.Serialize(new
        {
            source = "mock-fintech",
            scenario_hash = FabricSql.StableHash(scenario),
            table = "fintech_ai.risk_summary",
            representative_kpis = new[]
            {
                new { metric_name = "FXポジション損益合計", metric_value = -68000000.0, metric_unit = "JPY", description = "為替変動の直接影響" },
                new { metric_name = "ローン延滞率", metric_value = 1.9, metric_unit = "%", description = "信用リスクの先行指標" }
            }
        }));

    public Task<string> GetDetailedKpisAsync(string scenario, CancellationToken ct = default) =>
        Task.FromResult(JsonSerializer.Serialize(new
        {
            source = "mock-fintech",
            scenario_hash = FabricSql.StableHash(scenario),
            table = "fintech_ai.risk_summary",
            detailed_kpis = new[]
            {
                new { metric_name = "海外カード決済額", metric_value = 310000000.0, metric_unit = "JPY" },
                new { metric_name = "リボ払い残高", metric_value = 870000000.0, metric_unit = "JPY" },
                new { metric_name = "与信審査否認率", metric_value = 7.6, metric_unit = "%" }
            }
        }));
}
