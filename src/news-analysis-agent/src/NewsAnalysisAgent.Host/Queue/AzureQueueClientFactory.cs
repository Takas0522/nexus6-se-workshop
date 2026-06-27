using Azure.Storage.Queues;

namespace NewsAnalysisAgent.Host.Queue;

public sealed class AzureQueueClientFactory(QueueServiceClient queueServiceClient) : IQueueClientFactory
{
    public IQueueClient CreateClient(string queueName) => new AzureQueueClient(queueServiceClient.GetQueueClient(queueName));
}

public sealed class DisabledQueueClientFactory : IQueueClientFactory
{
    public IQueueClient CreateClient(string queueName) =>
        throw new InvalidOperationException("Storage:Account is not configured; queue polling is disabled.");
}

internal sealed class AzureQueueClient(QueueClient queueClient) : IQueueClient
{
    public async Task CreateIfNotExistsAsync(CancellationToken ct) =>
        await queueClient.CreateIfNotExistsAsync(cancellationToken: ct);

    public async Task<QueueJobMessage?> ReceiveMessageAsync(TimeSpan visibilityTimeout, CancellationToken ct)
    {
        var response = await queueClient.ReceiveMessageAsync(visibilityTimeout, ct);
        var message = response.Value;
        return message is null
            ? null
            : new QueueJobMessage(message.MessageId, message.PopReceipt, message.MessageText);
    }

    public async Task DeleteMessageAsync(string messageId, string popReceipt, CancellationToken ct) =>
        await queueClient.DeleteMessageAsync(messageId, popReceipt, ct);
}
