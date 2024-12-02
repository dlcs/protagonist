using DLCS.Model.Assets;

namespace Engine.Infrastructure.Messaging;

public interface IBatchCompletedNotificationSender
{
    /// <summary>
    /// Broadcast batch completed notification
    /// </summary>
    Task SendBatchCompletedMessages(IQueryable<Batch> completedBatches, CancellationToken cancellationToken = default);
}