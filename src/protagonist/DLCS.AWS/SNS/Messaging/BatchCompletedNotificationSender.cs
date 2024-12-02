using DLCS.Model.Assets;
using Microsoft.Extensions.Logging;

namespace DLCS.AWS.SNS.Messaging;

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
            await SendBatchCompletedMessage(batch, cancellationToken);
        }
    }

    public async Task SendBatchCompletedMessage(Batch batch, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Sending notification of creation of batch {Batch}", batch.Id);
        
        var batchCompletedNotification = new BatchCompletedNotification(batch);
        await topicPublisher.PublishToBatchCompletedTopic(batchCompletedNotification, cancellationToken);
    }
}