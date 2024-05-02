using Amazon.Runtime.Internal.Transform;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using DLCS.AWS.Settings;
using DLCS.Core;
using DLCS.Model.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DLCS.AWS.SNS;

public class TopicPublisher : ITopicPublisher
{
    private readonly IAmazonSimpleNotificationService snsClient;
    private readonly ILogger<TopicPublisher> logger;
    private readonly SNSSettings snsSettings;

    public TopicPublisher(IAmazonSimpleNotificationService snsClient,
        IOptions<AWSSettings> settings,
        ILogger<TopicPublisher> logger)
    {
        this.snsClient = snsClient;
        this.logger = logger;
        snsSettings = settings.Value.SNS;
    }
    
    /// <inheritdoc />
    public async Task<bool> PublishToAssetModifiedTopic(IReadOnlyList<AssetModifiedNotification> messages,
        CancellationToken cancellationToken = default)
    {
        if (messages.Count == 1)
        {
            var singleMessage = messages[0];
            return await PublishToAssetModifiedTopic(singleMessage, messages[0].EngineNotified, cancellationToken);
        }

        const int maxSnsBatchSize = 10;
        var allBatchSuccess = true;
        var batchIdPrefix = Guid.NewGuid();
        logger.LogDebug("Publishing SNS batch {BatchPrefix} containing {ItemCount} items", batchIdPrefix,
            messages.Count);
        var batchNumber = 0;
        foreach (var chunk in messages.Chunk(maxSnsBatchSize))
        {
            var success = await PublishBatch(chunk, batchIdPrefix, batchNumber++, cancellationToken);
            if (allBatchSuccess) allBatchSuccess = success;
        }
        
        logger.LogDebug("Published SNS batch {BatchPrefix} containing {ItemCount} items", batchIdPrefix,
            messages.Count);
        return allBatchSuccess;
    }
    
    private async Task<bool> PublishToAssetModifiedTopic(AssetModifiedNotification message, bool engineNotified,
        CancellationToken cancellationToken = default)
    {
        var request = new PublishRequest
        {
            TopicArn = snsSettings.AssetModifiedNotificationTopicArn,
            Message = message.MessageContents,
            MessageAttributes = GetMessageAttributes(message.ChangeType, engineNotified)
        };

        try
        {
            var response = await snsClient.PublishAsync(request, cancellationToken);
            return response.HttpStatusCode.IsSuccess();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending message to {Topic}", snsSettings.AssetModifiedNotificationTopicArn);
            return false;
        }
    }

    private async Task<bool> PublishBatch(AssetModifiedNotification[] chunk, Guid batchIdPrefix, int batchNumber,
        CancellationToken cancellationToken)
    {
        try
        {
            int batchCount = 0;
            var bulkRequest = new PublishBatchRequest
            {
                TopicArn = snsSettings.AssetModifiedNotificationTopicArn,
                PublishBatchRequestEntries = chunk.Select(m => new PublishBatchRequestEntry
                {
                    MessageAttributes = GetMessageAttributes(m.ChangeType, m.EngineNotified),
                    Message = m.MessageContents,
                    Id = $"{batchIdPrefix}_{batchNumber}_{batchCount++}",
                }).ToList()
            };

            var response = await snsClient.PublishBatchAsync(bulkRequest, cancellationToken);
            return response.HttpStatusCode.IsSuccess();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error publishing batch {BatchNumber} for {BatchPrefix}", batchNumber, batchIdPrefix);
            return false;
        }
    }
    
    private static Dictionary<string, MessageAttributeValue> GetMessageAttributes(ChangeType changeType, bool engineNotified)
    {
        var attributeValue = new MessageAttributeValue
        {
            StringValue = changeType.ToString(),
            DataType = "String"
        };
        var messageAttributes = new Dictionary<string, MessageAttributeValue>
        {
            { "messageType", attributeValue }
        };
        
        if (engineNotified)
        {
            messageAttributes.Add(new KeyValuePair<string, MessageAttributeValue>("EngineNotified",
                new MessageAttributeValue()
                {
                    DataType = "String",
                    StringValue = "True"
                }));
        }

        return messageAttributes;
    }
}