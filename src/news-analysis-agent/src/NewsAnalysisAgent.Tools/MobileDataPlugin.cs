using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace NewsAnalysisAgent.Tools;

public interface IMobileDataPlugin
{
    Task<string> GetRepresentativeKpisAsync(string scenario, CancellationToken ct = default);
    Task<string> GetDetailedKpisAsync(string scenario, CancellationToken ct = default);
}

public sealed class MobileDataPlugin(
    IConfiguration configuration,
    MockMobileDataPlugin fallback,
    ILogger<MobileDataPlugin> logger) : IMobileDataPlugin
{
    public Task<string> GetRepresentativeKpisAsync(string scenario, CancellationToken ct = default) =>
        QueryOrFallbackAsync("mobile_ai.risk_summary", scenario, "representative", fallback.GetRepresentativeKpisAsync, ct);

    public Task<string> GetDetailedKpisAsync(string scenario, CancellationToken ct = default) =>
        QueryOrFallbackAsync("mobile_ai.risk_summary", scenario, "detail", fallback.GetDetailedKpisAsync, ct);

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
            return await FabricSql.ReadRiskSummaryJsonAsync(command, "fabric-mobile", tableName, scenario, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Fabric Mobile KPI query failed. Falling back to mock data.");
            return await fallbackQuery(scenario, ct);
        }
    }
}

public sealed class MockMobileDataPlugin : IMobileDataPlugin
{
    public Task<string> GetRepresentativeKpisAsync(string scenario, CancellationToken ct = default) =>
        Task.FromResult(JsonSerializer.Serialize(new
        {
            source = "mock-mobile",
            scenario_hash = FabricSql.StableHash(scenario),
            table = "mobile_ai.risk_summary",
            representative_kpis = new[]
            {
                new { metric_name = "MNP転出率", metric_value = 3.4, metric_unit = "%", description = "競合施策影響の監視指標" },
                new { metric_name = "端末補助額合計", metric_value = 125000000.0, metric_unit = "JPY", description = "端末コスト上昇時の原資" }
            }
        }));

    public Task<string> GetDetailedKpisAsync(string scenario, CancellationToken ct = default) =>
        Task.FromResult(JsonSerializer.Serialize(new
        {
            source = "mock-mobile",
            scenario_hash = FabricSql.StableHash(scenario),
            table = "mobile_ai.risk_summary",
            detailed_kpis = new[]
            {
                new { metric_name = "海外仕入コスト合計", metric_value = 480000000.0, metric_unit = "JPY" },
                new { metric_name = "分割払い残高合計", metric_value = 2180000000.0, metric_unit = "JPY" },
                new { metric_name = "解約問い合わせ件数", metric_value = 1840.0, metric_unit = "件" }
            }
        }));
}
