using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace NewsAnalysisAgent.Tools;

internal static class FabricSql
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string? BuildConnectionString(IConfiguration configuration)
    {
        var explicitConnectionString = configuration.GetConnectionString("Fabric")
            ?? configuration["Fabric:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(explicitConnectionString))
        {
            return explicitConnectionString;
        }

        var endpoint = configuration["Fabric:SqlEndpoint"];
        var database = configuration["Fabric:Database"];
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(database))
        {
            return null;
        }

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = endpoint.Contains(',') ? endpoint : $"tcp:{endpoint},1433",
            InitialCatalog = database,
            Encrypt = true,
            TrustServerCertificate = false,
            ConnectTimeout = 5
        };
        builder["Authentication"] = "Active Directory Default";
        return builder.ConnectionString;
    }

    public static async Task<string> ReadRiskSummaryJsonAsync(
        SqlCommand command,
        string source,
        string tableName,
        string scenario,
        CancellationToken ct)
    {
        var rows = new List<Dictionary<string, object?>>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = await reader.IsDBNullAsync(i, ct) ? null : reader.GetValue(i);
            }

            rows.Add(row);
        }

        return JsonSerializer.Serialize(new
        {
            source,
            table = tableName,
            scenario_hash = StableHash(scenario),
            rows
        }, JsonOptions);
    }

    public static string StableHash(string value)
    {
        var hash = 2166136261u;
        foreach (var b in Encoding.UTF8.GetBytes(value))
        {
            hash ^= b;
            hash *= 16777619;
        }

        return hash.ToString("x8");
    }
}
