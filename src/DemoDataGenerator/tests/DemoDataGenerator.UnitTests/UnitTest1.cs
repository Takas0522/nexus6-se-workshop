using DemoDataGenerator.Data;
using DemoDataGenerator.Data.Contexts;
using DemoDataGenerator.Data.Seeding;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DemoDataGenerator.UnitTests;

public sealed class DemoDataGenerationTests
{
    [Theory]
    [InlineData(ScenarioKind.Fx)]
    [InlineData(ScenarioKind.Competitor)]
    [InlineData(ScenarioKind.BojRateHike)]
    public async Task Scenario_generation_creates_schema_counts_and_integrity(ScenarioKind scenario)
    {
        var dbPath = NewDatabasePath($"scenario-{scenario}");
        await using var contexts = CreateContexts(dbPath);
        var service = new DemoDataGenerationService(contexts.Common, contexts.Mobile, contexts.Ecommerce, contexts.Fintech);

        await service.EnsureSchemaAsync(recreateLocalSqlite: true);
        var summary = await service.GenerateAsync(new DemoDataRequest(scenario, CustomerCount: 300, Months: 1, Seed: 123, UseCopilot: false));

        Assert.Equal(300, summary.Customers);
        Assert.True(await contexts.Common.UnifiedCustomers.CountAsync() >= 100);
        Assert.Equal(300, await contexts.Mobile.Customers.CountAsync());
        Assert.Equal(300, await contexts.Mobile.Contracts.CountAsync());
        Assert.True(await contexts.Ecommerce.Members.CountAsync() >= 100);
        Assert.True(await contexts.Ecommerce.Orders.CountAsync() >= 100);
        Assert.True(await contexts.Fintech.Accounts.CountAsync() >= 100);
        Assert.True(await contexts.Fintech.CreditReviews.CountAsync() >= 100);

        var invalidMobileContracts = await contexts.Mobile.Contracts.CountAsync(c => !contexts.Mobile.Customers.Any(m => m.CustomerId == c.CustomerId));
        var invalidOrders = await contexts.Ecommerce.Orders.CountAsync(o => !contexts.Ecommerce.Members.Any(m => m.MemberId == o.MemberId) || !contexts.Ecommerce.Products.Any(p => p.Sku == o.Sku));
        var invalidCreditReviews = await contexts.Fintech.CreditReviews.CountAsync(r => !contexts.Fintech.Accounts.Any(a => a.UserId == r.UserId));
        Assert.Equal(0, invalidMobileContracts);
        Assert.Equal(0, invalidOrders);
        Assert.Equal(0, invalidCreditReviews);

        var tables = await ReadTableNamesAsync(dbPath);
        Assert.Contains("unified_customers", tables);
        Assert.Contains("mobile_contracts", tables);
        Assert.Contains("orders", tables);
        Assert.Contains("accounts", tables);
    }

    [Fact]
    public async Task Same_seed_generates_deterministic_data()
    {
        var first = await GenerateSnapshotAsync(NewDatabasePath("deterministic-a"));
        var second = await GenerateSnapshotAsync(NewDatabasePath("deterministic-b"));

        Assert.Equal(first, second);
    }

    private static async Task<string> GenerateSnapshotAsync(string dbPath)
    {
        await using var contexts = CreateContexts(dbPath);
        var service = new DemoDataGenerationService(contexts.Common, contexts.Mobile, contexts.Ecommerce, contexts.Fintech);
        await service.EnsureSchemaAsync(recreateLocalSqlite: true);
        await service.GenerateAsync(new DemoDataRequest(ScenarioKind.Fx, CustomerCount: 300, Months: 1, Seed: 777, UseCopilot: false));

        var customer = await contexts.Common.UnifiedCustomers.OrderBy(c => c.UnifiedCustomerId).FirstAsync();
        var contract = await contexts.Mobile.Contracts.OrderBy(c => c.ContractId).FirstAsync();
        var order = await contexts.Ecommerce.Orders.OrderBy(o => o.OrderId).FirstAsync();
        var position = await contexts.Fintech.FxPositions.OrderBy(p => p.PositionId).FirstAsync();
        return string.Join("|", customer.UnifiedCustomerId, customer.Region, contract.CustomerId, contract.SubsidyAmount, order.OrderAmount, position.PnlAmount);
    }

    private static ContextBundle CreateContexts(string dbPath)
    {
        var options = new DemoDataConnectionOptions(DemoDataTarget.LocalSqlite, $"Data Source={dbPath}");
        return new ContextBundle(new CommonDbContext(options), new MobileDbContext(options), new EcommerceDbContext(options), new FintechDbContext(options));
    }

    private static string NewDatabasePath(string name)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "generated-test-dbs");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"{name}-{Guid.NewGuid():N}.db");
    }

    private static async Task<HashSet<string>> ReadTableNamesAsync(string dbPath)
    {
        await using var connection = new SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table'";
        await using var reader = await command.ExecuteReaderAsync();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync())
            names.Add(reader.GetString(0));
        return names;
    }

    private sealed class ContextBundle(CommonDbContext common, MobileDbContext mobile, EcommerceDbContext ecommerce, FintechDbContext fintech) : IAsyncDisposable
    {
        public CommonDbContext Common { get; } = common;
        public MobileDbContext Mobile { get; } = mobile;
        public EcommerceDbContext Ecommerce { get; } = ecommerce;
        public FintechDbContext Fintech { get; } = fintech;

        public async ValueTask DisposeAsync()
        {
            await Common.DisposeAsync();
            await Mobile.DisposeAsync();
            await Ecommerce.DisposeAsync();
            await Fintech.DisposeAsync();
        }
    }
}
