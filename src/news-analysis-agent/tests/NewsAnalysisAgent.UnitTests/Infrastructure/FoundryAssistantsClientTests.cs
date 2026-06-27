using System.Net;
using System.Text;
using Azure.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NewsAnalysisAgent.Agents.BusinessImpact;
using NewsAnalysisAgent.Agents.Infrastructure;

namespace NewsAnalysisAgent.UnitTests.Infrastructure;

public sealed class FoundryAssistantsClientTests
{
    [Fact]
    public async Task InvokeAsync_RunsAssistantAndMergesFileSearchCitationIntoDataReferences()
    {
        var handler = new StubFoundryHandler();
        var client = new FoundryAssistantsClient(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Foundry:ProjectEndpoint"] = "https://fd-partneriq.services.ai.azure.com/api/projects/proj-PartnerIQ",
                    ["Foundry:DefaultModelDeployment"] = "gpt-5.4",
                    ["Foundry:FileSearchVectorStoreId"] = "vs_EN0WyOWKa7aVn0STee8oFhZ7",
                    ["Foundry:Assistant:ImpactAssistantId"] = "asst-impact"
                })
                .Build(),
            new StubHttpClientFactory(handler),
            new StubCredential(),
            NullLogger<FoundryAssistantsClient>.Instance,
            new MockFoundryAgentClient());

        var response = await client.InvokeAsync(BusinessImpactPrompts.SystemPrompt, "{}", [], CancellationToken.None);

        Assert.Contains("mobile_skill_competitor-mnp.md", response);
        Assert.Contains("/api/projects/proj-PartnerIQ/threads", handler.RequestPaths);
        Assert.Contains("/api/projects/proj-PartnerIQ/threads/thread_1/runs", handler.RequestPaths);
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler) { BaseAddress = new Uri("https://fd-partneriq.services.ai.azure.com") };
    }

    private sealed class StubCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            new("stub-token", DateTimeOffset.UtcNow.AddHours(1));

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            ValueTask.FromResult(GetToken(requestContext, cancellationToken));
    }

    private sealed class StubFoundryHandler : HttpMessageHandler
    {
        public List<string> RequestPaths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestPaths.Add(request.RequestUri!.AbsolutePath);
            var path = request.RequestUri!.AbsolutePath;
            var method = request.Method;
            var body = (method.Method, path) switch
            {
                ("POST", "/api/projects/proj-PartnerIQ/threads") => """{"id":"thread_1"}""",
                ("POST", "/api/projects/proj-PartnerIQ/threads/thread_1/messages") => """{"id":"msg_1"}""",
                ("POST", "/api/projects/proj-PartnerIQ/threads/thread_1/runs") => """{"id":"run_1","status":"queued"}""",
                ("GET", "/api/projects/proj-PartnerIQ/threads/thread_1/runs/run_1") => """{"id":"run_1","status":"completed"}""",
                ("GET", "/api/projects/proj-PartnerIQ/threads/thread_1/runs/run_1/steps") => """
                    {"data":[{"step_details":{"tool_calls":[{"type":"file_search"}]}}]}
                    """,
                ("GET", "/api/projects/proj-PartnerIQ/threads/thread_1/messages") => """
                    {
                      "data": [
                        {
                          "role": "assistant",
                          "content": [
                            {
                              "type": "text",
                              "text": {
                                "value": "{\"impact_scores\":[{\"division\":\"mobile\",\"score\":4.0,\"risk_level\":\"high\"},{\"division\":\"ecommerce\",\"score\":2.0,\"risk_level\":\"medium\"},{\"division\":\"fintech\",\"score\":1.0,\"risk_level\":\"low\"}],\"impact_reasons\":[\"mobile: MNP risk [source: mobile_skill_competitor-mnp.md]\"],\"priority_order\":[\"mobile\",\"ecommerce\",\"fintech\"]}",
                                "annotations": [
                                  {"type":"file_citation","file_citation":{"file_id":"file_1"}}
                                ]
                              }
                            }
                          ]
                        }
                      ]
                    }
                    """,
                ("GET", "/api/projects/proj-PartnerIQ/files/file_1") => """{"id":"file_1","filename":"mobile_skill_competitor-mnp.md"}""",
                _ => throw new InvalidOperationException($"Unexpected request {method} {path}")
            };

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
