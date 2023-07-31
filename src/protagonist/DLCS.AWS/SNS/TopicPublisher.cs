using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using DLCS.AWS.Settings;
using DLCS.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DLCS.AWS.SNS;

public class TopicPublisher : ITopicPublisher, IDisposable
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
    public async Task<bool> PublishToTopicAsync(string messageContents, SubscribedQueueType subscribedQueueType, CancellationToken cancellationToken = default)
    {
        CreateTopicResponse? topic; 
        try
        {
            // Retrieving the ARN of a topic from AWS requires calling CreateTopic
            topic = await client.CreateTopicAsync(sNSSettings.DeleteNotificationTopicName, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving topic {Topic}", sNSSettings.DeleteNotificationTopicName);
            return false;
        }

        var attributeValue = new MessageAttributeValue()
        {
            StringValue = subscribedQueueType.ToString(),
            DataType = "String"
        };

        var request = new PublishRequest
        {
            TopicArn = topic.TopicArn,
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
            logger.LogError(ex, "Error sending message to {Topic}", topic);
            return false;
        }
    }

    public void Dispose()
    {
        client.Dispose();
    }
}