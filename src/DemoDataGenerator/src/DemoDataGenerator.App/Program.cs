using DemoDataGenerator.Data;
using DemoDataGenerator.Data.Contexts;
using DemoDataGenerator.Data.Copilot;
using DemoDataGenerator.Data.Seeding;

namespace DemoDataGenerator.App;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Contains("--help") || args.Contains("-h"))
        {
            PrintHelp();
            return 0;
        }

        try
        {
            var options = CliOptions.Parse(args);
            var request = new DemoDataRequest(options.Scenario, options.CustomerCount, options.Months, options.Seed, options.UseCopilot);
            ICopilotPromptRunner runner = options.UseCopilot ? new EnvironmentCopilotPromptRunner() : new TemplateCopilotPromptRunner();
            await runner.RunAsync($"scenario={request.Scenario};customers={request.CustomerCount}");

            var common = new CommonDbContext(new DemoDataConnectionOptions(options.Target, options.CommonConnectionString));
            var mobile = new MobileDbContext(new DemoDataConnectionOptions(options.Target, options.MobileConnectionString));
            var ecommerce = new EcommerceDbContext(new DemoDataConnectionOptions(options.Target, options.EcommerceConnectionString));
            var fintech = new FintechDbContext(new DemoDataConnectionOptions(options.Target, options.FintechConnectionString));
            await using (common)
            await using (mobile)
            await using (ecommerce)
            await using (fintech)
            {
                var service = new DemoDataGenerationService(common, mobile, ecommerce, fintech);
                await service.EnsureSchemaAsync(options.Target == DemoDataTarget.LocalSqlite);
                if (options.Target == DemoDataTarget.FabricSql)
                    await service.ClearDataAsync();
                var summary = await service.GenerateAsync(request);
                Console.WriteLine($"Generated scenario={summary.Scenario}, customers={summary.Customers}, mobile_contracts={summary.MobileContracts}, ecommerce_orders={summary.EcommerceOrders}, fintech_accounts={summary.FintechAccounts}");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine("Use --help for usage.");
            return 1;
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine("DemoDataGenerator");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --project src/DemoDataGenerator.App -- --scenario 1 --target local-sqlite --db-path ./demo.db --scale small");
        Console.WriteLine("  dotnet run --project src/DemoDataGenerator.App -- --scenario 1 --target fabric-sql --conn-prefix \"Server=...;Database={dbname};Authentication=Active Directory Default\"");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --scenario <1|2|3|fx|competitor|boj-rate-hike>");
        Console.WriteLine("  --target <local-sqlite|fabric-sql>");
        Console.WriteLine("  --db-path <path>                 SQLite path for local-sqlite (default: ./demo.db)");
        Console.WriteLine("  --conn-prefix <connection>       Fabric SQL connection; {dbname} is replaced per domain");
        Console.WriteLine("  --common-db-name <name>          Fabric SQL physical database name for common (default: sqldb_common_01)");
        Console.WriteLine("  --mobile-db-name <name>          Fabric SQL physical database name for mobile (default: sqldb_mobile_01)");
        Console.WriteLine("  --ecommerce-db-name <name>       Fabric SQL physical database name for ecommerce (default: sqldb_ecommerce_01)");
        Console.WriteLine("  --fintech-db-name <name>         Fabric SQL physical database name for fintech (default: sqldb_fintech_01)");
        Console.WriteLine("  --scale <small|medium|large|N>   small=300 customers and one month (default)");
        Console.WriteLine("  --seed <N>                       deterministic seed (default: 42)");
        Console.WriteLine("  --use-copilot                    opt in; falls back to template generation without GITHUB_TOKEN");
        Console.WriteLine("  --help");
    }

    private sealed class CliOptions
    {
        public ScenarioKind Scenario { get; private init; } = ScenarioKind.Fx;
        public DemoDataTarget Target { get; private init; } = DemoDataTarget.LocalSqlite;
        public int CustomerCount { get; private init; } = 300;
        public int Months { get; private init; } = 1;
        public int Seed { get; private init; } = 42;
        public bool UseCopilot { get; private init; }
        public string CommonConnectionString { get; private init; } = string.Empty;
        public string MobileConnectionString { get; private init; } = string.Empty;
        public string EcommerceConnectionString { get; private init; } = string.Empty;
        public string FintechConnectionString { get; private init; } = string.Empty;

        public static CliOptions Parse(string[] args)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (!arg.StartsWith("--", StringComparison.Ordinal))
                    throw new ArgumentException($"Unexpected argument '{arg}'.");
                var key = arg[2..];
                if (key == "use-copilot")
                {
                    flags.Add(key);
                    continue;
                }
                if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                    throw new ArgumentException($"Missing value for --{key}.");
                values[key] = args[++i];
            }

            var scenario = ParseScenario(Get(values, "scenario", "1")!);
            var target = ParseTarget(Get(values, "target", "local-sqlite")!);
            var (customers, months) = ParseScale(Get(values, "scale", "small")!);
            var seed = int.Parse(Get(values, "seed", "42")!);
            if (target == DemoDataTarget.LocalSqlite)
            {
                var dbPath = Get(values, "db-path", "./demo.db")!;
                var fullPath = Path.GetFullPath(dbPath);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory());
                var connection = $"Data Source={fullPath}";
                return new CliOptions { Scenario = scenario, Target = target, CustomerCount = customers, Months = months, Seed = seed, UseCopilot = flags.Contains("use-copilot"), CommonConnectionString = connection, MobileConnectionString = connection, EcommerceConnectionString = connection, FintechConnectionString = connection };
            }

            var prefix = Get(values, "conn-prefix", null) ?? throw new ArgumentException("--conn-prefix is required for fabric-sql.");
            var commonDbName = Get(values, "common-db-name", "sqldb_common_01")!;
            var mobileDbName = Get(values, "mobile-db-name", "sqldb_mobile_01")!;
            var ecommerceDbName = Get(values, "ecommerce-db-name", "sqldb_ecommerce_01")!;
            var fintechDbName = Get(values, "fintech-db-name", "sqldb_fintech_01")!;
            return new CliOptions
            {
                Scenario = scenario,
                Target = target,
                CustomerCount = customers,
                Months = months,
                Seed = seed,
                UseCopilot = flags.Contains("use-copilot"),
                CommonConnectionString = BuildFabricConnection(prefix, commonDbName),
                MobileConnectionString = BuildFabricConnection(prefix, mobileDbName),
                EcommerceConnectionString = BuildFabricConnection(prefix, ecommerceDbName),
                FintechConnectionString = BuildFabricConnection(prefix, fintechDbName)
            };
        }

        private static string? Get(Dictionary<string, string> values, string key, string? fallback) => values.TryGetValue(key, out var value) ? value : fallback;

        private static ScenarioKind ParseScenario(string value) => value.ToLowerInvariant() switch
        {
            "1" or "fx" or "fx-shock" => ScenarioKind.Fx,
            "2" or "competitor" or "one-pass" => ScenarioKind.Competitor,
            "3" or "boj-rate-hike" or "rate-hike" => ScenarioKind.BojRateHike,
            _ => throw new ArgumentException("--scenario must be 1, 2, 3, fx, competitor, or boj-rate-hike.")
        };

        private static DemoDataTarget ParseTarget(string value) => value.ToLowerInvariant() switch
        {
            "local-sqlite" => DemoDataTarget.LocalSqlite,
            "fabric-sql" => DemoDataTarget.FabricSql,
            _ => throw new ArgumentException("--target must be local-sqlite or fabric-sql.")
        };

        private static (int Customers, int Months) ParseScale(string value) => value.ToLowerInvariant() switch
        {
            "small" => (300, 1),
            "medium" => (1000, 1),
            "large" => (3000, 3),
            _ when int.TryParse(value, out var customers) && customers > 0 => (customers, 1),
            _ => throw new ArgumentException("--scale must be small, medium, large, or a positive integer.")
        };

        private static string BuildFabricConnection(string template, string dbName) => template.Contains("{dbname}", StringComparison.OrdinalIgnoreCase)
            ? template.Replace("{dbname}", dbName, StringComparison.OrdinalIgnoreCase)
            : template;
    }
}
