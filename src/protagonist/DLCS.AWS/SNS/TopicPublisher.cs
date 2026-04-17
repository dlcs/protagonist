using System.Text.Json;
using Amazon;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using DLCS.AWS.Settings;
using DLCS.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DLCS.AWS.SNS;

public class TopicPublisher(
    IAmazonSimpleNotificationService snsClient,
    IOptions<AWSSettings> settings,
    ILogger<TopicPublisher> logger)
    : ITopicPublisher
{
    private readonly SNSSettings snsSettings = settings.Value.SNS;
    private readonly JsonSerializerOptions settings = new(JsonSerializerDefaults.Web);
    
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
    public Task<bool> PublishToBatchCompletedTopic(BatchCompletedNotification message,
        CancellationToken cancellationToken)
        => PublishBatchCompleted(message, snsSettings.BatchCompletedTopicArn,
            nameof(snsSettings.BatchCompletedTopicArn), cancellationToken);

    /// <inheritdoc />
    public Task<bool> PublishToAdjunctBatchCompletedTopic(AdjunctBatchCompletedNotification message,
        CancellationToken cancellationToken)
        => PublishBatchCompleted(message, snsSettings.AdjunctBatchCompletedTopicArn,
            nameof(snsSettings.AdjunctBatchCompletedTopicArn), cancellationToken);

    /// <inheritdoc />
    public async Task<bool> PublishToDeliverableModifiedTopic(IReadOnlyList<DeliverableModifiedNotification> messages,
        DeliverableTopicType topicType, CancellationToken cancellationToken = default)
    {
        var topicArn = topicType == DeliverableTopicType.Asset
            ? snsSettings.AssetModifiedNotificationTopicArn!
            : snsSettings.AdjunctModifiedNotificationTopicArn!;
        
        if (messages.Count == 1)
        {
            var singleMessage = messages[0];
            return await PublishToDeliverableModifiedTopic(singleMessage, topicArn, cancellationToken);
        }

        const int maxSnsBatchSize = 5;
        var allBatchSuccess = true;
        var batchIdPrefix = Guid.NewGuid();
        if (!Arn.TryParse(topicArn, out var arn))
        {
            logger.LogError("Could not parse the topic arn {Arn} into a valid arn", topicArn);
            return false;
        }
        logger.LogDebug("Publishing SNS batch {BatchPrefix} containing {ItemCount} items to {Service}", batchIdPrefix,
            messages.Count, arn.Resource);
        var batchNumber = 0;
        foreach (var chunk in messages.Chunk(maxSnsBatchSize))
        {
            var success = await PublishBatch(chunk, batchIdPrefix, batchNumber++, topicArn, cancellationToken);
            if (allBatchSuccess) allBatchSuccess = success;
        }
        
        logger.LogTrace("Published SNS batch {BatchPrefix} containing {ItemCount} items", batchIdPrefix,
            messages.Count);
        return allBatchSuccess;
    }

    private Task<bool> PublishToDeliverableModifiedTopic(DeliverableModifiedNotification message, string topicArn,
        CancellationToken cancellationToken = default)
    {
        var request = new PublishRequest
        {
            TopicArn = topicArn,
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

    private async Task<bool> PublishBatch(DeliverableModifiedNotification[] chunk, Guid batchIdPrefix, int batchNumber,
        string topicArn, CancellationToken cancellationToken)
    {
        try
        {
            int batchCount = 0;
            var bulkRequest = new PublishBatchRequest
            {
                TopicArn = topicArn,
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
        => attributes.ToDictionary(
            attribute => attribute.Key,
            attribute => new MessageAttributeValue { DataType = "String", StringValue = attribute.Value });

    private async Task<bool> PublishBatchCompleted(IBatchCompletedNotification message, string? topicArn, string topicName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(topicArn))
        {
            logger.LogWarning("{TopicName} is not set - cannot publish batch completed notification", topicName);
            return false;
        }
        
        var request = new PublishRequest
        {
            TopicArn = topicArn,
            Message = JsonSerializer.Serialize(message, message.GetType(), settings),
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                ["CustomerId"] = new()
                {
                    StringValue = message.Customer.ToString(),
                    DataType = "String"
                },
            }
        };

        return await TryPublishRequest(request, cancellationToken);
    }
}
