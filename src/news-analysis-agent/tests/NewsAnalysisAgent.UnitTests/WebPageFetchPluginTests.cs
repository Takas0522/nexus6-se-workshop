using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using NewsAnalysisAgent.Tools;

namespace NewsAnalysisAgent.UnitTests;

public sealed class WebPageFetchPluginTests
{
    [Fact]
    public async Task FetchAsync_ExtractsReadableTextFromHtml()
    {
        var html = """
        <html><head><style>.x{}</style><script>alert(1)</script><title>T</title></head>
        <body><main><h1>円安ニュース</h1><p>本文 &amp; 追加情報</p></main></body></html>
        """;
        var handler = new StaticHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(html, System.Text.Encoding.UTF8, "text/html")
        });
        var plugin = new WebPageFetchPlugin(new HttpClient(handler), NullLogger<WebPageFetchPlugin>.Instance);

        var text = await plugin.FetchAsync("https://example.com/news", CancellationToken.None);

        Assert.Contains("円安ニュース", text);
        Assert.Contains("本文 & 追加情報", text);
        Assert.DoesNotContain("alert", text);
    }

    private sealed class StaticHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response);
    }
}
