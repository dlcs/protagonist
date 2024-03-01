using System.Collections.Generic;
using System.IO.Enumeration;
using DLCS.Core.Caching;
using DLCS.Model.DeliveryChannels;
using DLCS.Model.Policies;
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

    public List<DeliveryChannelPolicy> MatchedDeliveryChannels(string mediaType, int space, int customerId)
    {
        var completedMatch = new List<DeliveryChannelPolicy>();
        
        var orderedDefaultDeliveryChannels = OrderedDefaultDeliveryChannels(space, customerId);
        
        foreach (var defaultDeliveryChannel in orderedDefaultDeliveryChannels)
        {
            if (completedMatch.Any(d => d.Channel == defaultDeliveryChannel.DeliveryChannelPolicy.Channel))
            {
                continue;
            }

            if (FileSystemName.MatchesSimpleExpression(defaultDeliveryChannel.MediaType, mediaType))
            {
                completedMatch.Add(defaultDeliveryChannel.DeliveryChannelPolicy);
            }
        }

        return completedMatch;
    }

    public DeliveryChannelPolicy MatchDeliveryChannelPolicyForChannel(
        string mediaType, 
        int space, 
        int customerId, 
        string? channel)
    {
        var orderedDefaultDeliveryChannels = OrderedDefaultDeliveryChannels(space, customerId, channel);
        
        foreach (var defaultDeliveryChannel in orderedDefaultDeliveryChannels)
        {
            if (FileSystemName.MatchesSimpleExpression(defaultDeliveryChannel.MediaType, mediaType))
            {
                return defaultDeliveryChannel.DeliveryChannelPolicy;
            }
        }

        throw new InvalidOperationException($"Failed to match media type {mediaType} to channel {channel}");
    }
    
    private List<DefaultDeliveryChannel> GetDefaultDeliveryChannelsForCustomer(int customerId, int space)
    {
        var key = $"defaultDeliveryChannels:{customerId}";
        
        var defaultDeliveryChannels = appCache.GetOrAdd(key, () =>
        {
            logger.LogDebug("Refreshing {CacheKey} from database", key);

            var defaultDeliveryChannels = dlcsContext.DefaultDeliveryChannels
                .AsNoTracking()
                .Include(d => d.DeliveryChannelPolicy)
                .Where(d => d.Customer == customerId).ToList();

            return defaultDeliveryChannels;
        }, cacheSettings.GetMemoryCacheOptions(CacheDuration.Long));

        return defaultDeliveryChannels.Where(d => d.Space == space || d.Space == 0).ToList();
    }
    
    private List<DefaultDeliveryChannel> OrderedDefaultDeliveryChannels(int space, int customerId, string? channel = null)
    {
        var defaultDeliveryChannels = GetDefaultDeliveryChannelsForCustomer(customerId, space)
            .Where(d => channel == null || d.DeliveryChannelPolicy.Channel == channel);

        return defaultDeliveryChannels.OrderByDescending(v => v.Space)
            .ThenByDescending(c => c.MediaType.Length).ToList();
    }
}