using DLCS.AWS.SNS;
using DLCS.Model.Customers;
using Microsoft.Extensions.Logging;

namespace API.Infrastructure.Messaging;

public class CustomerNotificationSender : ICustomerNotificationSender
{
    private readonly ITopicPublisher topicPublisher;
    private readonly ILogger<CustomerNotificationSender> logger;

    public CustomerNotificationSender(ITopicPublisher topicPublisher, ILogger<CustomerNotificationSender> logger)
    {
        this.logger = logger;
        this.topicPublisher = topicPublisher;
    }

    public async Task SendCustomerCreatedMessage(Customer newCustomer, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Sending notification of creation of customer {CustomerId}", newCustomer.Id);
        
        var createdCustomer = new CustomerCreatedNotification(newCustomer);
        await topicPublisher.PublishToCustomerCreatedTopic(createdCustomer, cancellationToken);
    }
}