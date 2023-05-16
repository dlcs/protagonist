using System.Text.Json;
using DLCS.AWS.SQS;
using DLCS.Model.Messaging;
using DLCS.Model.Processing;
using Engine.Ingest.Models;

namespace Engine.Ingest;

/// <summary>
/// Handler for ingest messages that have been pulled from queue.
/// </summary>
public class IngestHandler : IMessageHandler
{
    private readonly IAssetIngester ingester;
    private readonly ICustomerQueueRepository customerQueueRepository;
    private readonly ILogger<IngestHandler> logger;
    private readonly JsonSerializerOptions settings = new(JsonSerializerDefaults.Web);

    public IngestHandler(IAssetIngester ingester, ICustomerQueueRepository customerQueueRepository, 
        ILogger<IngestHandler> logger)
    {
        this.ingester = ingester;
        this.customerQueueRepository = customerQueueRepository;
        this.logger = logger;
    }
    
    public async Task<bool> HandleMessage(QueueMessage message, CancellationToken cancellationToken)
    {
        IngestResult ingestResult;
        if (IsLegacyMessageType(message))
        {
            var legacyEvent = DeserializeBody<LegacyIngestEvent>(message);
            if (legacyEvent == null) return false;
            ingestResult = await ingester.Ingest(legacyEvent, cancellationToken);
        }
        else
        {
            var ingestEvent = DeserializeBody<IngestAssetRequest>(message);
            if (ingestEvent == null) return false;
            ingestResult = await ingester.Ingest(ingestEvent, cancellationToken);
        }

        logger.LogDebug("Message {MessageId} handled with result {IngestResult}", message.MessageId, ingestResult.Status);

        await UpdateCustomerQueue(message, cancellationToken, ingestResult);

        // return true so that the message is deleted from the queue in all instances.
        // This shouldn't be the case and can be revisited at a later date as it will need logic of how Batch.Errors is
        // calculated
        return true;
    }

    private async Task UpdateCustomerQueue(QueueMessage message, CancellationToken cancellationToken,
        IngestResult ingestResult)
    {
        var queue = message.QueueName.ToLower().Contains("priority") ? QueueNames.Priority : QueueNames.Default;
        int customer = 0;
        try
        {
            if (ingestResult.Asset != null)
            {
                customer = ingestResult.Asset.Customer; 
                await customerQueueRepository.DecrementSize(ingestResult.Asset.Customer, queue,
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
            return message.Body.Deserialize<T>(settings);
        }
        catch (JsonException jsonException)
        {
            logger.LogError(jsonException, "Error converting message {MessageId} to {TargetType}", message.MessageId,
                typeof(T).Name);
            return null;
        }
    }

    // If the message contains "_type" field then it is the legacy version from Deliverator/Inversion
    private bool IsLegacyMessageType(QueueMessage message) => message.Body.ContainsKey("_type");
}