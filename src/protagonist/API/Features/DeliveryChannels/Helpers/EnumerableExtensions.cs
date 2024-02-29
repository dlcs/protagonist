using System.Collections.Generic;
using DLCS.Model.Policies;

namespace API.Features.DeliveryChannels.Helpers;

public static class EnumerableExtensions
{
    private const int AdminCustomer = 1;
    
    public static DeliveryChannelPolicy RetrieveDeliveryChannel(this IEnumerable<DeliveryChannelPolicy> policies, int  customerId, string channel, string policy)
    {
        return policies.Single(p =>
            (p.Customer == customerId &&
             p.System == false &&
             p.Channel == channel &&
             p.Name == policy
                 .Split('/', StringSplitOptions.None).Last()) ||
            (p.Customer == AdminCustomer &&
             p.System == true &&
             p.Channel == channel &&
             p.Name == policy));
    }
}