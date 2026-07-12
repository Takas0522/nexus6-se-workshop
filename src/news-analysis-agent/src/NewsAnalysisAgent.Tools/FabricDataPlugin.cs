using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace NewsAnalysisAgent.Tools;

public interface IFabricDataPlugin
{
    Task<string> QueryBusinessKpisAsync(CancellationToken ct = default);
    Task<string> GetMonthlyRevenueAsync(string yearMonth, CancellationToken ct = default);
    Task<string> GetDivisionKpiSnapshotAsync(string division, string yearMonth, CancellationToken ct = default);
}

public sealed class FabricDataPlugin(
    IConfiguration configuration,
    MockFabricDataPlugin fallback,
    ILogger<FabricDataPlugin> logger) : IFabricDataPlugin
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = false };

    public Task<string> QueryBusinessKpisAsync(CancellationToken ct = default) =>
        GetMonthlyRevenueAsync(DateTimeOffset.UtcNow.ToString("yyyy-MM"), ct);

    public async Task<string> GetMonthlyRevenueAsync(string yearMonth, CancellationToken ct = default)
    {
        var connectionString = BuildConnectionString();
        if (connectionString is null)
        {
            logger.LogWarning("Fabric SQL endpoint or database is not configured. Falling back to mock Fabric data.");
            return await fallback.GetMonthlyRevenueAsync(yearMonth, ct);
        }

        const string sql = """
SELECT year_month, division, gross_revenue_jpy, total_cost_jpy, gross_margin_jpy,
       gross_margin_rate, active_customer_count, churn_rate
FROM kpi_monthly_revenue
WHERE year_month = @yearMonth
ORDER BY division;
""";

        try
        {
            var rows = await QueryRowsAsync(connectionString, sql, [new SqlParameter("@yearMonth", yearMonth)], ct);
            return JsonSerializer.Serialize(new { source = "fabric", table = "kpi_monthly_revenue", year_month = yearMonth, rows }, JsonOptions);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Fabric monthly revenue query failed. Falling back to mock Fabric data.");
            return await fallback.GetMonthlyRevenueAsync(yearMonth, ct);
        }
    }

    public async Task<string> GetDivisionKpiSnapshotAsync(string division, string yearMonth, CancellationToken ct = default)
    {
        var normalizedDivision = NormalizeDivision(division);
        var connectionString = BuildConnectionString();
        if (connectionString is null)
        {
            logger.LogWarning("Fabric SQL endpoint or database is not configured. Falling back to mock Fabric data.");
            return await fallback.GetDivisionKpiSnapshotAsync(normalizedDivision, yearMonth, ct);
        }

        var tableName = normalizedDivision + "_ai_risk_summary";
        var sql = $"""
SELECT TOP (50) year_month, metric_name, metric_value, metric_unit, description
FROM {tableName}
WHERE year_month = @yearMonth
ORDER BY metric_name;
""";

        try
        {
            var rows = await QueryRowsAsync(connectionString, sql, [new SqlParameter("@yearMonth", yearMonth)], ct);
            return JsonSerializer.Serialize(new { source = "fabric", table = tableName, division = normalizedDivision, year_month = yearMonth, rows }, JsonOptions);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Fabric {Division} KPI snapshot query failed. Falling back to mock Fabric data.", normalizedDivision);
            return await fallback.GetDivisionKpiSnapshotAsync(normalizedDivision, yearMonth, ct);
        }
    }

    private string? BuildConnectionString()
    {
        var endpoint = configuration["Fabric:SqlEndpoint"];
        var database = configuration["Fabric:Database"];
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(database))
        {
            return null;
        }

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = endpoint.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase) || endpoint.Contains(',')
                ? endpoint
                : $"tcp:{endpoint},1433",
            InitialCatalog = database,
            Encrypt = true,
            TrustServerCertificate = false,
            ConnectTimeout = 15,
            Authentication = SqlAuthenticationMethod.ActiveDirectoryDefault
        };
        return builder.ConnectionString;
    }

    private static async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryRowsAsync(
        string connectionString,
        string sql,
        IReadOnlyList<SqlParameter> parameters,
        CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection) { CommandType = CommandType.Text };
        command.Parameters.AddRange(parameters.ToArray());

        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<IReadOnlyDictionary<string, object?>>();
        while (await reader.ReadAsync(ct))
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }

            rows.Add(row);
        }

        return rows;
    }

    private static string NormalizeDivision(string division) => division.Trim().ToLowerInvariant() switch
    {
        "mobile" or "モバイル" or "携帯電話" => "mobile",
        "ecommerce" or "ec" or "eコマース" => "ecommerce",
        "fintech" or "フィンテック" or "金融" => "fintech",
        "entertainment" or "エンターテイメント" => "entertainment",
        "game" or "ゲーム" => "game",
        "sns" or "ソーシャル" => "sns",
        "telecom" or "通信" => "telecom",
        "media" or "メディア" => "media",
        "advertising" or "広告" => "advertising",
        "insurance" or "保険" => "insurance",
        "realestate" or "不動産" => "realestate",
        "education" or "教育" => "education",
        "healthcare" or "医療" or "ヘルスケア" => "healthcare",
        "manufacturing" or "製造" => "manufacturing",
        "logistics" or "物流" => "logistics",
        "retail" or "小売" => "retail",
        var d => d.Replace(" ", "_")
    };
}

public sealed class MockFabricDataPlugin : IFabricDataPlugin
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<string> QueryBusinessKpisAsync(CancellationToken ct = default) =>
        GetMonthlyRevenueAsync(DateTimeOffset.UtcNow.ToString("yyyy-MM"), ct);

    public Task<string> GetMonthlyRevenueAsync(string yearMonth, CancellationToken ct = default) =>
        Task.FromResult(JsonSerializer.Serialize(new
        {
            source = "mock-fabric",
            table = "kpi_monthly_revenue",
            year_month = yearMonth,
            rows = new[]
            {
                new { division = "mobile", gross_revenue_jpy = 1250000000m, total_cost_jpy = 870000000m, gross_margin_jpy = 380000000m, gross_margin_rate = 0.304m, fx_exposure_usd = 3400000m, fx_exposure_other_jpy = 92000000m, active_customer_count = 30000, churned_customer_count = 820 },
                new { division = "ecommerce", gross_revenue_jpy = 940000000m, total_cost_jpy = 705000000m, gross_margin_jpy = 235000000m, gross_margin_rate = 0.250m, fx_exposure_usd = 2100000m, fx_exposure_other_jpy = 148000000m, active_customer_count = 20000, churned_customer_count = 510 },
                new { division = "fintech", gross_revenue_jpy = 760000000m, total_cost_jpy = 456000000m, gross_margin_jpy = 304000000m, gross_margin_rate = 0.400m, fx_exposure_usd = 5200000m, fx_exposure_other_jpy = 184000000m, active_customer_count = 15000, churned_customer_count = 260 }
            }
        }, JsonOptions));

    public Task<string> GetDivisionKpiSnapshotAsync(string division, string yearMonth, CancellationToken ct = default)
    {
        var normalized = division.Trim().ToLowerInvariant();
        var metrics = normalized switch
        {
            "mobile" => new[]
            {
                new { metric_name = "mnp_out_rate", metric_value = 0.041m, metric_unit = "%", description = "MNP転出率" },
                new { metric_name = "device_fx_cost_jpy", metric_value = 185000000m, metric_unit = "JPY", description = "海外端末仕入コスト" }
            },
            "ecommerce" => new[]
            {
                new { metric_name = "campaign_roi", metric_value = 1.24m, metric_unit = "ratio", description = "キャンペーンROI" },
                new { metric_name = "crossborder_fx_cost_jpy", metric_value = 126000000m, metric_unit = "JPY", description = "越境EC為替コスト" }
            },
            "fintech" => new[]
            {
                new { metric_name = "fx_position_usd", metric_value = 5200000m, metric_unit = "USD", description = "USD建てFXポジション" },
                new { metric_name = "loan_delinquency_rate", metric_value = 0.018m, metric_unit = "%", description = "ローン延滞率" }
            },
            _ => []
        };

        return Task.FromResult(JsonSerializer.Serialize(new
        {
            source = "mock-fabric",
            table = normalized + "_ai.risk_summary",
            division = normalized,
            year_month = yearMonth,
            rows = metrics
        }, JsonOptions));
    }
}
