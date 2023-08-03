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
    private IAmazonSimpleNotificationService client;
    private ILogger<TopicPublisher> logger;
    private SNSSettings sNSSettings;

    public TopicPublisher(IAmazonSimpleNotificationService client, ILogger<TopicPublisher> logger, IOptions<AWSSettings> settings)
    {
        this.client = client;
        this.logger = logger;
        sNSSettings = settings.Value.SNS;
    }
    
    /// <inheritdoc />
    public async Task<bool> PublishToAssetModifiedTopic(string messageContents, ChangeType changeType, CancellationToken cancellationToken = default)
    {
        try
        {
            var topic = await client.GetTopicAttributesAsync(
                new GetTopicAttributesRequest(sNSSettings.AssetModifiedNotificationTopicArn), cancellationToken);

            if (!topic.HttpStatusCode.IsSuccess())
            {
                logger.LogError("Could not retrieve topic details for topic {Topic}",
                    sNSSettings.AssetModifiedNotificationTopicArn);
                return false;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving details of topic {Topic}", 
                sNSSettings.AssetModifiedNotificationTopicArn);
            return false;
        }

        var attributeValue = new MessageAttributeValue()
        {
            StringValue = changeType.ToString(),
            DataType = "String"
        };

        var request = new PublishRequest
        {
            TopicArn = sNSSettings.AssetModifiedNotificationTopicArn,
            Message = messageContents,
            MessageAttributes = new Dictionary<string, MessageAttributeValue>()
            {
                {"messageType", attributeValue}
            }
        };

        try
        {
            var response = await client.PublishAsync(request, cancellationToken);
            return response.HttpStatusCode.IsSuccess();
        }
        catch(Exception ex)
        {
            logger.LogError(ex, "Error sending message to {Topic}", sNSSettings.AssetModifiedNotificationTopicArn);
            return false;
        }
    }
}