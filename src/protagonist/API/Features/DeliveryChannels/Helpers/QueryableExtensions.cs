using System.Collections.Generic;
using DLCS.Model.Policies;
using Microsoft.EntityFrameworkCore;

namespace API.Features.DeliveryChannels.Helpers;

public static class QueryableExtensions
{
    private const int AdminCustomer = 1;

    /// <summary>
    /// Find matching policy, this will be either (in order of precedence):
    /// Non system policy where channel and name match OR
    /// System policy where channel and name match.
    ///
    /// NOTE: will throw a <see cref="InvalidOperationException"/> if no match found
    /// </summary>
    /// <exception cref="InvalidOperationException"> if record not found</exception>
    public static DeliveryChannelPolicy RetrieveDeliveryChannel(this IEnumerable<DeliveryChannelPolicy> policies,
        int customerId, string channel, string policy)
    {
        return policies.Single(p =>
            (p.Customer == customerId &&
             p.System == false &&
             p.Channel == channel &&
             p.Name == policy
                 .Split('/').Last()) ||
            (p.Customer == AdminCustomer &&
             p.System &&
             p.Channel == channel &&
             p.Name == policy));
    }

    /// <summary>
    /// Find exact matching policy
    /// </summary>
    public static Task<DeliveryChannelPolicy?> GetDeliveryChannel(this IQueryable<DeliveryChannelPolicy> policies,
        int customerId, string channel, string policy, CancellationToken cancellationToken)
    {
        return policies.SingleOrDefaultAsync(p =>
                p.Customer == customerId &&
                p.Channel == channel &&
                p.Name == policy,
            cancellationToken);
    }
}
