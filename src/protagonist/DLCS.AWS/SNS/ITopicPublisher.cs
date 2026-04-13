using DLCS.Model.Customers;

namespace DLCS.AWS.SNS;

public interface ITopicPublisher
{
    /// <summary>
    /// Asynchronously publishes a message to Customer created topic
    /// </summary>
    /// <returns>Boolean representing the overall success/failure status of request</returns>
    public Task<bool> PublishToCustomerCreatedTopic(CustomerCreatedNotification message,
        CancellationToken cancellationToken);
    
    /// <summary>
    /// Asynchronously publishes a message to the Batch completed topic
    /// </summary>
    /// <returns>Boolean representing the overall success/failure status of request</returns>
    public Task<bool> PublishToBatchCompletedTopic(BatchCompletedNotification message,
        CancellationToken cancellationToken);

    /// <summary>
    /// Asynchronously publishes a message to the Batch completed topic for an AdjunctBatch
    /// </summary>
    /// <returns>Boolean representing the overall success/failure status of request</returns>
    public Task<bool> PublishToAdjunctBatchCompletedTopic(AdjunctBatchCompletedNotification message,
        CancellationToken cancellationToken);

    /// <summary>
    /// Asynchronously publishes a message to deliverable modified SNS
    /// </summary>
    /// <param name="messages">A collection of notifications to send</param>
    /// <param name="topicType">The type of topic to publish to</param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <returns>Boolean representing the overall success/failure status of all requests</returns>
    Task<bool> PublishToDeliverableModifiedTopic(IReadOnlyList<DeliverableModifiedNotification> messages,
        DeliverableTopicType topicType, CancellationToken cancellationToken);
}

/// <summary>
/// Represents the contents + type of change for Deliverable modified notification
/// </summary>
public record DeliverableModifiedNotification(string MessageContents, Dictionary<string, string> Attributes);

/// <summary>
/// Represents contents of CustomerCreation notification
/// </summary>
public class CustomerCreatedNotification
{
    public string Name { get; private set; }

    public int Id { get; private set; }
    
    public CustomerCreatedNotification(Customer customer)
    {
        Id = customer.Id;
        Name = customer.Name;
    }
};
