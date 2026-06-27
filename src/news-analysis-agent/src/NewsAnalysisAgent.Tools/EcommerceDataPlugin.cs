using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace NewsAnalysisAgent.Tools;

public interface IEcommerceDataPlugin
{
    Task<string> GetRepresentativeKpisAsync(string scenario, CancellationToken ct = default);
    Task<string> GetDetailedKpisAsync(string scenario, CancellationToken ct = default);
}

public sealed class EcommerceDataPlugin(
    IConfiguration configuration,
    MockEcommerceDataPlugin fallback,
    ILogger<EcommerceDataPlugin> logger) : IEcommerceDataPlugin
{
    public Task<string> GetRepresentativeKpisAsync(string scenario, CancellationToken ct = default) =>
        QueryOrFallbackAsync("ecommerce_ai.risk_summary", scenario, "representative", fallback.GetRepresentativeKpisAsync, ct);

    public Task<string> GetDetailedKpisAsync(string scenario, CancellationToken ct = default) =>
        QueryOrFallbackAsync("ecommerce_ai.risk_summary", scenario, "detail", fallback.GetDetailedKpisAsync, ct);

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
            return await FabricSql.ReadRiskSummaryJsonAsync(command, "fabric-ecommerce", tableName, scenario, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Fabric Ecommerce KPI query failed. Falling back to mock data.");
            return await fallbackQuery(scenario, ct);
        }
    }
}

public sealed class MockEcommerceDataPlugin : IEcommerceDataPlugin
{
    public Task<string> GetRepresentativeKpisAsync(string scenario, CancellationToken ct = default) =>
        Task.FromResult(JsonSerializer.Serialize(new
        {
            source = "mock-ecommerce",
            scenario_hash = FabricSql.StableHash(scenario),
            table = "ecommerce_ai.risk_summary",
            representative_kpis = new[]
            {
                new { metric_name = "粗利率", metric_value = 24.8, metric_unit = "%", description = "カテゴリ別収益性の代表指標" },
                new { metric_name = "カート離脱率", metric_value = 38.2, metric_unit = "%", description = "会員行動の変化検知" }
            }
        }));

    public Task<string> GetDetailedKpisAsync(string scenario, CancellationToken ct = default) =>
        Task.FromResult(JsonSerializer.Serialize(new
        {
            source = "mock-ecommerce",
            scenario_hash = FabricSql.StableHash(scenario),
            table = "ecommerce_ai.risk_summary",
            detailed_kpis = new[]
            {
                new { metric_name = "越境EC仕入コスト", metric_value = 93000000.0, metric_unit = "JPY" },
                new { metric_name = "ポイント還元コスト合計", metric_value = 42000000.0, metric_unit = "JPY" },
                new { metric_name = "キャンペーンROI", metric_value = 1.42, metric_unit = "ratio" }
            }
        }));
}
