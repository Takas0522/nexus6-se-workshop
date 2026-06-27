using System.Text.Json;
using Microsoft.Extensions.Options;
using NewsAnalysisAgent.Models;
using NewsAnalysisAgent.Orchestration;

namespace NewsAnalysisAgent.Host.Queue;

public sealed class QueueBackgroundService(
    IConfiguration configuration,
    IQueueClientFactory queueClientFactory,
    IWorkflow<NewsAnalysisContext> workflow,
    ILogger<QueueBackgroundService> logger) : BackgroundService
{
    public const string QueueName = "news-analysis-jobs";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(configuration["Storage:Account"]))
        {
            logger.LogWarning("Storage:Account is not configured. Queue polling is skipped.");
            return;
        }

        var queueClient = queueClientFactory.CreateClient(QueueName);
        await queueClient.CreateIfNotExistsAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = await ExecuteOnePollAsync(queueClient, stoppingToken);
            if (!processed)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    public async Task<bool> ExecuteOnePollAsync(IQueueClient queueClient, CancellationToken ct)
    {
        var message = await queueClient.ReceiveMessageAsync(TimeSpan.FromMinutes(5), ct);
        if (message is null)
        {
            return false;
        }

        var context = CreateContext(message.MessageText);

        await workflow.RunAsync(context, ct);
        await queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt, ct);
        return true;
    }

    private static NewsAnalysisContext CreateContext(string messageText)
    {
        try
        {
            var job = JsonSerializer.Deserialize<NewsAnalysisJob>(messageText, JsonOptions);
            var text = job?.OriginalNewsText ?? job?.NewsText;
            if (!string.IsNullOrWhiteSpace(text))
            {
                return new NewsAnalysisContext
                {
                    OriginalNewsText = text,
                    SearchHints = job?.SearchHints ?? []
                };
            }
        }
        catch (JsonException)
        {
            // Plain text queue messages are accepted for local smoke tests.
        }

        return new NewsAnalysisContext { OriginalNewsText = messageText };
    }
}
