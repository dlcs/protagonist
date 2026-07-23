using System.Text.Json;
using DLCS.AWS.SQS;
using DLCS.Model.Messaging;
using DLCS.Model.Processing;
using DLCS.Web.Logging;
using static DLCS.AWS.SQS.SqsQueueUtilities.Constants.MessageAttributeNames;

namespace Engine.Ingest;

/// <summary>
/// Handler for ingest messages that have been pulled from queue.
/// </summary>
public class IngestHandler(
    IAssetIngester assetIngester,
    IAdjunctIngester adjunctIngester,
    ICustomerQueueRepository customerQueueRepository,
    ILogger<IngestHandler> logger)
    : IMessageHandler
{
    public async Task<bool> HandleMessage(QueueMessage message, CancellationToken cancellationToken)
    {
        _ = message.MessageAttributes.TryGetValue(IngestType, out var ingestType);

        return ingestType switch
        {
            IngestAssetRequest.IngestType => await HandleIngest<IngestAssetRequest>(message, assetIngester.Ingest,
                cancellationToken),
            IngestAdjunctRequest.IngestType => await HandleIngest<IngestAdjunctRequest>(message, adjunctIngester.Ingest,
                cancellationToken),
            _ => false
        };
    }

    private async Task<bool> HandleIngest<T>(QueueMessage message,
        Func<T, CancellationToken, Task<IngestResult>> ingester, CancellationToken cancellationToken) where T : class
    {
        var ingestEvent = DeserializeBody<T>(message);
        if (ingestEvent == null)
        {
            return false;
        }

        using (LogContextHelpers.SetCorrelationId(message.MessageId))
        {
            var ingestResult = await ingester.Invoke(ingestEvent, cancellationToken);

            logger.LogDebug("Message {MessageId} handled with result {IngestResult}", message.MessageId,
                ingestResult.Status);
            await UpdateCustomerQueue(message, ingestResult, GetQueueName<T>(message), cancellationToken);
        }

        // return true so that the message is deleted from the queue in all instances.
        // This shouldn't be the case and can be revisited at a later date as it will need logic of how Batch.Errors
        // property is calculated

        return true;
    }

    private static string GetQueueName<T>(QueueMessage message) => message switch
    {
        _ when typeof(T) == typeof(IngestAdjunctRequest) => QueueNames.Adjunct,
        _ when message.QueueName.Contains("priority", StringComparison.OrdinalIgnoreCase) => QueueNames.Priority,
        _ => QueueNames.Default
    };

    private async Task UpdateCustomerQueue(QueueMessage message,
        IngestResult ingestResult, string queue, CancellationToken cancellationToken)
    {
        var customer = 0;
        try
        {
            if (ingestResult.AssetId != null)
            {
                customer = ingestResult.AssetId.Customer;
                await customerQueueRepository.DecrementSize(ingestResult.AssetId.Customer, queue,
                    cancellationToken: cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error decrementing customer {Customer} queue {QueueName}", customer, queue);
        }
    }

    private T? DeserializeBody<T>(QueueMessage message)
        where T : class
    {
        try
        {
            return message.GetMessageContents<T>();
        }
        catch (JsonException jsonException)
        {
            logger.LogError(jsonException, "Error converting message {MessageId} to {TargetType}", message.MessageId,
                typeof(T).Name);
            return null;
        }
    }
}
