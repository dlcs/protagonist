using System.Collections.Generic;
using DLCS.Model.Policies;

namespace API.Features.DeliveryChannels.Helpers;

public static class QueryableExtensions
{
    private const int AdminCustomer = 1;
    
    /// <summary>
    /// Find matching policy, this will be either (in order of precedence):
    /// Non system policy where channel and name match OR
    /// System policy where channel and name match.
    ///
    /// NOTE: will throw InvalidOperationException if no match found
    /// </summary>
    /// <exception cref="InvalidOperationException"> if record not found</exception>
    public static DeliveryChannelPolicy RetrieveDeliveryChannel(this IEnumerable<DeliveryChannelPolicy> policies, int customerId, string channel, string policy)
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