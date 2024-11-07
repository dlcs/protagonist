using DLCS.Model.Customers;

namespace DLCS.AWS.SNS;

public interface ITopicPublisher
{
    /// <summary>
    /// Asynchronously publishes a message to an Asset Modified SNS topic
    /// </summary>
    /// <param name="messages">A collection of notifications to send</param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <returns>Boolean representing the overall success/failure status of all requests</returns>
    public Task<bool> PublishToAssetModifiedTopic(IReadOnlyList<AssetModifiedNotification> messages,
        CancellationToken cancellationToken);

    /// <summary>
    /// Asynchronously publishes a message to Customer created topic
    /// </summary>
    /// <returns>Boolean representing the overall success/failure status of request</returns>
    public Task<bool> PublishToCustomerCreatedTopic(CustomerCreatedNotification message,
        CancellationToken cancellationToken);
}

/// <summary>
/// Represents the contents + type of change for Asset modified notification
/// </summary>
public record AssetModifiedNotification(string MessageContents, Dictionary<string, string> Attributes);

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