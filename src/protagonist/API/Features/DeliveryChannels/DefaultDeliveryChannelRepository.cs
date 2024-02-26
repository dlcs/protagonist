using System.Collections.Generic;
using DLCS.Core.Caching;
using DLCS.Model.DeliveryChannels;
using DLCS.Repository;
using LazyCache;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace API.Features.DeliveryChannels;

public class DefaultDeliveryChannelRepository : IDefaultDeliveryChannelRepository
{
    private readonly IAppCache appCache;
    private readonly CacheSettings cacheSettings;
    private readonly ILogger<DefaultDeliveryChannelRepository> logger;
    private readonly DlcsContext dlcsContext;

    public DefaultDeliveryChannelRepository(
        IAppCache appCache,
        ILogger<DefaultDeliveryChannelRepository> logger,
        IOptions<CacheSettings> cacheOptions,
        DlcsContext dlcsContext)
    {
        this.appCache = appCache;
        this.logger = logger;
        cacheSettings = cacheOptions.Value;
        this.dlcsContext = dlcsContext;
    }
    
    public List<DefaultDeliveryChannel> GetDefaultDeliveryChannelsForCustomer(int customerId, int space)
    {
        var key = $"defaultDeliveryChannels:{customerId}";
        
        var defaultDeliveryChannels = appCache.GetOrAdd(key, () =>
        {
            logger.LogDebug("Refreshing {CacheKey} from database", key);

            var defaultDeliveryChannels = dlcsContext.DefaultDeliveryChannels
                .AsNoTracking()
                .Include(d => d.DeliveryChannelPolicy)
                .Where(d => d.Customer == customerId);

            return defaultDeliveryChannels;
        }, cacheSettings.GetMemoryCacheOptions(CacheDuration.Long));

        return defaultDeliveryChannels.Where(d => d.Space == space || d.Space == 0).ToList();
    }
}