using System.Text.Json;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using DLCS.AWS.Settings;
using DLCS.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DLCS.AWS.SNS;

public class TopicPublisher : ITopicPublisher
{
    private readonly IAmazonSimpleNotificationService snsClient;
    private readonly ILogger<TopicPublisher> logger;
    private readonly SNSSettings snsSettings;
    private readonly JsonSerializerOptions settings = new(JsonSerializerDefaults.Web);

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
            return await PublishToAssetModifiedTopic(singleMessage, cancellationToken);
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

    /// <inheritdoc />
    public async Task<bool> PublishToCustomerCreatedTopic(CustomerCreatedNotification message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(snsSettings.CustomerCreatedTopicArn))
        {
            logger.LogWarning("Customer Created Topic Arn is not set - cannot send CustomerCreatedNotification");
            return false;
        }
        
        var request = new PublishRequest
        {
            TopicArn = snsSettings.CustomerCreatedTopicArn,
            Message = JsonSerializer.Serialize(message, settings),
        };

        return await TryPublishRequest(request, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> PublishToBatchCompletedTopic(BatchCompletedNotification message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(snsSettings.BatchCompletedTopicArn))
        {
            logger.LogWarning("Batch Completed Topic Arn is not set - cannot send BatchCompletedNotification");
            return false;
        }
        
        var request = new PublishRequest
        {
            TopicArn = snsSettings.BatchCompletedTopicArn,
            Message = JsonSerializer.Serialize(message, settings),
            MessageAttributes = new Dictionary<string, MessageAttributeValue>()
            {
                {"CustomerId", new MessageAttributeValue
                {
                    StringValue = message.Customer.ToString(),
                    DataType = "String"
                }}
            } 
        };

        return await TryPublishRequest(request, cancellationToken);
    }

    private Task<bool> PublishToAssetModifiedTopic(AssetModifiedNotification message,
        CancellationToken cancellationToken = default)
    {
        var request = new PublishRequest
        {
            TopicArn = snsSettings.AssetModifiedNotificationTopicArn,
            Message = message.MessageContents,
            MessageAttributes = GetMessageAttributes(message.Attributes)
        };

        return TryPublishRequest(request, cancellationToken);
    }
    
    private async Task<bool> TryPublishRequest(PublishRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await snsClient.PublishAsync(request, cancellationToken);
            return response.HttpStatusCode.IsSuccess();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending message to {Topic}", request.TopicArn);
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
                    MessageAttributes = GetMessageAttributes(m.Attributes),
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

    private static Dictionary<string, MessageAttributeValue> GetMessageAttributes(Dictionary<string, string> attributes)
    {
        var messageAttributes = new Dictionary<string, MessageAttributeValue>();
        foreach (var attribute in attributes)
        {
            messageAttributes.Add(attribute.Key,
                new MessageAttributeValue
                {
                    DataType = "String",
                    StringValue = attribute.Value
                });
        }

        return messageAttributes;
    }
}
