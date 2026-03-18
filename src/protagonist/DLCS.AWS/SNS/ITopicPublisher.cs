using DLCS.Model.Customers;
using DLCS.Model.Messaging.Adjunct;

namespace DLCS.AWS.SNS;

public interface ITopicPublisher
{
    /// <summary>
    /// Asynchronously publishes a message to an Asset Modified SNS topic
    /// </summary>
    /// <param name="messages">A collection of notifications to send</param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <returns>Boolean representing the overall success/failure status of all requests</returns>
    public Task<bool> PublishToAssetModifiedTopic(IReadOnlyList<DeliverableModifiedNotification> messages,
        CancellationToken cancellationToken);

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
    /// Asynchronously publishes a message to an Adjunct Modified SNS topic
    /// </summary>
    /// <param name="messages">A collection of notifications to send</param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <returns>Boolean representing the overall success/failure status of all requests</returns>
    Task<bool> PublishToAdjunctModifiedTopic(IReadOnlyList<DeliverableModifiedNotification> messages, CancellationToken cancellationToken);
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
