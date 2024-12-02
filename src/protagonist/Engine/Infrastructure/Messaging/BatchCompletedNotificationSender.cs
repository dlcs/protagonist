using DLCS.AWS.SNS;
using DLCS.Model.Assets;

namespace Engine.Infrastructure.Messaging;

public class BatchCompletedNotificationSender : IBatchCompletedNotificationSender
{
    private readonly ITopicPublisher topicPublisher;
    private readonly ILogger<BatchCompletedNotificationSender> logger;
    
    public BatchCompletedNotificationSender(ITopicPublisher topicPublisher, 
        ILogger<BatchCompletedNotificationSender> logger)
    {
        this.topicPublisher = topicPublisher;
        this.logger = logger;
    }
    
    public async Task SendBatchCompletedMessages(IQueryable<Batch> completedBatches, CancellationToken cancellationToken = default)
    {
        foreach (var batch in completedBatches)
        {
            logger.LogDebug("Sending notification of creation of batch {Batch}", batch.Id);
        
            var batchCompletedNotification = new BatchCompletedNotification(batch);
            await topicPublisher.PublishToBatchCompletedTopic(batchCompletedNotification, cancellationToken);
        }
    }
}