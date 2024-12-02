using DLCS.Model.Assets;

namespace DLCS.AWS.SNS.Messaging;

public interface IBatchCompletedNotificationSender
{
    /// <summary>
    /// Broadcast multiple batch completed notification
    /// </summary>
    Task SendBatchCompletedMessages(IQueryable<Batch> completedBatches, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Broadcast batch completed notification
    /// </summary>
    Task SendBatchCompletedMessage(Batch completedBatch, CancellationToken cancellationToken = default);
}