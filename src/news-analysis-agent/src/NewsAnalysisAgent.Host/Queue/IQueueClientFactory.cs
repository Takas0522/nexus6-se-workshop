namespace NewsAnalysisAgent.Host.Queue;

public interface IQueueClientFactory
{
    IQueueClient CreateClient(string queueName);
}

public interface IQueueClient
{
    Task CreateIfNotExistsAsync(CancellationToken ct);
    Task<QueueJobMessage?> ReceiveMessageAsync(TimeSpan visibilityTimeout, CancellationToken ct);
    Task DeleteMessageAsync(string messageId, string popReceipt, CancellationToken ct);
}

public sealed record QueueJobMessage(string MessageId, string PopReceipt, string MessageText);
