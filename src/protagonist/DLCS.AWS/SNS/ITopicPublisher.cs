using DLCS.Model.Messaging;

namespace DLCS.AWS.SNS;

public interface ITopicPublisher
{
    /// <summary>
    /// Asynchronously publishes a message to a Asset Modified SNS topic
    /// </summary>
    /// <param name="messageContents">The contents of the message</param>
    /// <param name="changeType">The type of change that has happened</param>
    /// <param name="cancellationToken">Current cancellation token</param>
    public Task<bool> PublishToAssetModifiedTopic(string messageContents, ChangeType changeType,
        CancellationToken cancellationToken);

    /// <summary>
    /// Asynchronously publishes a message to a Asset Modified SNS topic
    /// </summary>
    /// <param name="messages">A collection of notifications to send</param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <returns>Boolean representing the overall success/failure status of all requests</returns>
    public Task<bool> PublishToAssetModifiedTopic(IReadOnlyList<AssetModifiedNotification> messages,
        CancellationToken cancellationToken);
}

/// <summary>
/// Represents the contents + type of change for Asset modified notification
/// </summary>
public record AssetModifiedNotification(string MessageContents, ChangeType ChangeType);