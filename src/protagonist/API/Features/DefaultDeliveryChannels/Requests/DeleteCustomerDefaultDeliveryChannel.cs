using DLCS.Core;
using DLCS.Model.DeliveryChannels;
using MediatR;

namespace API.Features.DefaultDeliveryChannels.Requests;

public class DeleteCustomerDefaultDeliveryChannel : IRequest<ResultMessage<DeleteResult>>
{
    public DeleteCustomerDefaultDeliveryChannel(int customer, string defaultDeliveryChannelId)
    {
        Customer = customer;
        DefaultDeliveryChannelId = defaultDeliveryChannelId;
    }
    
    public int Customer { get; }
    
    public string DefaultDeliveryChannelId { get; }
}