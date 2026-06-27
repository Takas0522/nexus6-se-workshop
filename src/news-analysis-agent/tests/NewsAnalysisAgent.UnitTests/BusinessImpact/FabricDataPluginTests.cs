using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NewsAnalysisAgent.Tools;

namespace NewsAnalysisAgent.UnitTests.BusinessImpact;

public sealed class FabricDataPluginTests
{
    [Fact]
    public async Task GetMonthlyRevenueAsync_FallsBackToMockWhenConnectionStringIsEmpty()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        var plugin = new FabricDataPlugin(configuration, new MockFabricDataPlugin(), NullLogger<FabricDataPlugin>.Instance);

        var json = await plugin.GetMonthlyRevenueAsync("2026-06", CancellationToken.None);

        Assert.Contains("mock-fabric", json);
        Assert.Contains("mobile", json);
        Assert.Contains("ecommerce", json);
        Assert.Contains("fintech", json);
    }
}
