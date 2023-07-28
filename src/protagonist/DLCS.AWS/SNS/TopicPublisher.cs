using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using DLCS.AWS.Settings;
using DLCS.Core;
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
    
    public async Task<bool> PublishToTopicAsync(string messageContents, CancellationToken cancellationToken = default)
    {
        // Retrieving the ARN of a topic from AWS requires calling CreateTopic
        var topic = await client.CreateTopicAsync(sNSSettings.DeleteNotificationTopicName, cancellationToken);
        
        var request = new PublishRequest
        {
            TopicArn = topic.TopicArn,
            Message = messageContents,
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
}