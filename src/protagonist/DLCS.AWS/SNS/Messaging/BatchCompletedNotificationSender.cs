using DLCS.Model.Assets;
using Microsoft.Extensions.Logging;

namespace DLCS.AWS.SNS.Messaging;

public class BatchCompletedNotificationSender(
    ITopicPublisher topicPublisher,
    ILogger<BatchCompletedNotificationSender> logger)
    : IBatchCompletedNotificationSender
{
    public async Task SendBatchCompletedMessage(IDeliverableBatch batch, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Sending notification of creation of batch {Type} {Batch}", batch.GetType().Name, batch.Id);

        switch (batch)
        {
            case Batch b:
                await topicPublisher.PublishToBatchCompletedTopic(new BatchCompletedNotification(b), cancellationToken);
                break;
            case AdjunctBatch ab:
                await topicPublisher.PublishToAdjunctBatchCompletedTopic(new AdjunctBatchCompletedNotification(ab), cancellationToken);
                break;
        }
    }
}
