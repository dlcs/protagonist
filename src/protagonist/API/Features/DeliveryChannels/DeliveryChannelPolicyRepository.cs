using DLCS.Core.Caching;
using DLCS.Model.DeliveryChannels;
using DLCS.Model.Policies;
using DLCS.Repository;
using LazyCache;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace API.Features.DeliveryChannels;

public class DeliveryChannelPolicyRepository : IDeliveryChannelPolicyRepository
{
    private readonly IAppCache appCache;
    private readonly CacheSettings cacheSettings;
    private readonly ILogger<DeliveryChannelPolicyRepository> logger;
    private readonly DlcsContext dlcsContext;
    private const int AdminCustomer = 1;

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

    public DeliveryChannelPolicy RetrieveDeliveryChannelPolicy(int customerId, string channel, string policy)
    {
        var key = $"deliveryChannelPolicies:{customerId}";
        
        var deliveryChannelPolicies =  appCache.GetOrAdd(key, () =>
        {
            logger.LogDebug("Refreshing {CacheKey} from database", key);

            var defaultDeliveryChannels = dlcsContext.DeliveryChannelPolicies
                .AsNoTracking()
                .Where(d => d.Customer == customerId || d.Customer == AdminCustomer).ToList();

            return defaultDeliveryChannels;
        }, cacheSettings.GetMemoryCacheOptions(CacheDuration.Long)); 
        
        return deliveryChannelPolicies.Single(p =>
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