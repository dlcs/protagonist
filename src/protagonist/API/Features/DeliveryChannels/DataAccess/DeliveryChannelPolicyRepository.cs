using System.Collections.Generic;
using API.Features.DeliveryChannels.Helpers;
using DLCS.Core.Caching;
using DLCS.Model.DeliveryChannels;
using DLCS.Model.Policies;
using DLCS.Repository;
using LazyCache;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace API.Features.DeliveryChannels.DataAccess;

public class DeliveryChannelPolicyRepository(
    IAppCache appCache,
    ILogger<DeliveryChannelPolicyRepository> logger,
    IOptions<CacheSettings> cacheOptions,
    DlcsContext dlcsContext)
    : IDeliveryChannelPolicyRepository
{
    private readonly CacheSettings cacheSettings = cacheOptions.Value;
    private const int AdminCustomer = 1;

    public async Task<DeliveryChannelPolicy> RetrieveDeliveryChannelPolicy(int customerId, string channel, string policy)
    {
        var deliveryChannelPolicies = await RetrieveFromCache(customerId);

        return deliveryChannelPolicies.RetrieveDeliveryChannel(customerId, channel, policy);
    }
    
    public async Task<DeliveryChannelPolicy> RetrieveDeliveryChannelPolicy(int customerId, string channel, int policyId)
    {
        var deliveryChannelPolicies = await RetrieveFromCache(customerId);

        return deliveryChannelPolicies.First(dcp => dcp.Id == policyId && dcp.Channel == channel);
    }

    private async Task<List<DeliveryChannelPolicy>> RetrieveFromCache(int customerId)
    {
        var key = CacheKeys.DeliveryChannelPolicies(customerId);

        var deliveryChannelPolicies = await appCache.GetOrAddAsync(key, async () =>
        {
            logger.LogDebug("Refreshing {CacheKey} from database", key);

            var defaultDeliveryChannels = await dlcsContext.DeliveryChannelPolicies
                .AsNoTracking()
                .Where(d => d.Customer == customerId || d.Customer == AdminCustomer)
                .ToListAsync();

            return defaultDeliveryChannels;
        }, cacheSettings.GetMemoryCacheOptions(CacheDuration.Long));
        return deliveryChannelPolicies;
    }
}
