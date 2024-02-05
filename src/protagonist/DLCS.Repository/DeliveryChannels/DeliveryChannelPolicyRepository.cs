using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Caching;
using DLCS.Model.DeliveryChannels;
using DLCS.Model.Policies;
using LazyCache;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DLCS.Repository.DeliveryChannels;

public class DeliveryChannelPolicyRepository : IDeliveryChannelPolicyRepository
{
    private readonly IAppCache appCache;
    private readonly CacheSettings cacheSettings;
    private readonly ILogger<DeliveryChannelPolicyRepository> logger;
    private readonly DlcsContext dlcsContext;
    
    public DeliveryChannelPolicyRepository(
        IAppCache appCache,
        ILogger<DeliveryChannelPolicyRepository> logger,
        IOptions<CacheSettings> cacheOptions,
        DlcsContext dlcsContext)
    {
        this.appCache = appCache;
        this.logger = logger;
        cacheSettings = cacheOptions.Value;
        this.dlcsContext = dlcsContext;
    }


    public async Task<DeliveryChannelPolicy?> GetDeliveryChannelPolicy(int customer, string policyName, string channel, CancellationToken cancellationToken = default)
    {
        try
        {
            var deliveryChannelPolicies = await GetDeliveryChannelPolicies(cancellationToken);
            return deliveryChannelPolicies.SingleOrDefault(p => p.Customer == customer && 
                                                                p.Name == policyName && 
                                                                p.Channel == channel);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error getting delivery channel policy for customer {Customer} with the name {Name} on channel {Channel}",
                customer, policyName, channel);
            return null;
        }
    }

    public async Task<bool> AddDeliveryChannelCustomerPolicies(int customerId, CancellationToken cancellationToken = default)
    {
        try
        {
            var deliveryChannelPolicies = await GetDeliveryChannelPolicies(cancellationToken);
            var policiesToCopy = new List<DeliveryChannelPolicy>(deliveryChannelPolicies.FindAll(p => p is { Customer: 1, System: false }));

            var maxId = deliveryChannelPolicies.Max(d => d.Id);

            var updatedPolicies = policiesToCopy.Select(deliveryChannelPolicy => new DeliveryChannelPolicy()
                {
                    Customer = customerId,
                    Channel = deliveryChannelPolicy.Channel,
                    DisplayName = deliveryChannelPolicy.DisplayName,
                    Name = deliveryChannelPolicy.Name,
                    PolicyData = deliveryChannelPolicy.PolicyData,
                    Created = deliveryChannelPolicy.Modified,
                    Modified = deliveryChannelPolicy.Modified,
                    Id = ++maxId
                })
                .ToList();

            await dlcsContext.DeliveryChannelPolicies.AddRangeAsync(updatedPolicies, cancellationToken);

            var updated = await dlcsContext.SaveChangesAsync(cancellationToken);

            if (updated > 0)
            {
                appCache.Remove("DeliveryChannelPolicies"); // db updated, so need to reset the cache
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error adding delivery channel policies to customer {Customer}", customerId);
            return false;
        }

        return true;
    }

    private Task<List<DeliveryChannelPolicy>> GetDeliveryChannelPolicies(CancellationToken cancellationToken)
    {
        const string key = "DeliveryChannelPolicies";
        return appCache.GetOrAddAsync(key, async () =>
        {
            logger.LogDebug("Refreshing DeliveryChannelPolicies from database");
            var deliveryChannelPolicies =
                await dlcsContext.DeliveryChannelPolicies.AsNoTracking().ToListAsync(cancellationToken: cancellationToken);
            return deliveryChannelPolicies;
        }, cacheSettings.GetMemoryCacheOptions());
    }
}