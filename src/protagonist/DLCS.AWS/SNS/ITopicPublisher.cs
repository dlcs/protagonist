using DLCS.Model.Messaging;

namespace DLCS.AWS.SNS;

public interface ITopicPublisher
{
    /// <summary>
    /// Asynchronously publishes a message to an SNS topic
    /// </summary>
    /// <param name="messageContents">The contents of the message</param>
    /// <param name="changeType">The type of change that has happened</param>
    /// <param name="cancellationToken">A cancellation token</param>
    /// ///
    public Task<bool> PublishToAssetModifiedTopic(string messageContents, ChangeType changeType,
        CancellationToken cancellationToken);
}