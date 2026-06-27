using Microsoft.AspNetCore.Mvc.Testing;

namespace NewsAnalysisAgent.IntegrationTests;

public sealed class HostSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HostSmokeTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Storage:Account", string.Empty);
            builder.UseSetting("DevUi:EnableManualTrigger", "true");
        });
    }

    [Fact]
    public async Task DevUi_ReturnsSuccess()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/devui");

        Assert.True(response.IsSuccessStatusCode);
    }
}
