namespace DLCS.AWS.SNS;

public interface ITopicPublisher
{
    /// <summary>
    /// Asynchronously publishes a message to an SNS topic
    /// </summary>
    /// <param name="messageContents">The contents of the message</param>
    ///  /// <param name="subscribedQueueType">The type of subscribed queue to publish a message to</param>
    /// <param name="cancellationToken">A cancellation token</param>
    public Task<bool> PublishToTopicAsync(string messageContents, SubscribedQueueType subscribedQueueType, CancellationToken cancellationToken);
}