using DLCS.Model.Customers;

namespace API.Infrastructure.Messaging;

public interface ICustomerNotificationSender
{
    /// <summary>
    /// Broadcast customer creation notification
    /// </summary>
    Task SendCustomerCreatedMessage(Customer newCustomer, CancellationToken cancellationToken = default);
}