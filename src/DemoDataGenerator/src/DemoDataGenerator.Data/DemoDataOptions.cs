namespace DemoDataGenerator.Data;

public enum ScenarioKind
{
    Fx = 1,
    Competitor = 2,
    BojRateHike = 3
}

public enum DemoDataTarget
{
    LocalSqlite,
    FabricSql
}

public sealed record DemoDataConnectionOptions(DemoDataTarget Target, string ConnectionString);

public sealed record DemoDataRequest(
    ScenarioKind Scenario,
    int CustomerCount,
    int Months,
    int Seed,
    bool UseCopilot);

public sealed record GenerationSummary(
    ScenarioKind Scenario,
    int Customers,
    int MobileContracts,
    int EcommerceOrders,
    int FintechAccounts);
