using System.Collections.Generic;
using DLCS.Core.Caching;
using DLCS.Model.DeliveryChannels;
using DLCS.Repository.Messaging;
using LazyCache;
using Microsoft.Extensions.Options;

namespace API.Features.DeliveryChannels;

public class AvChannelPolicyOptionsRepository : IAvChannelPolicyOptionsRepository
{
    private readonly IAppCache appCache;
    private readonly CacheSettings cacheSettings;
    private readonly IEngineClient engineClient;

    public AvChannelPolicyOptionsRepository(IAppCache appCache, IOptions<CacheSettings> cacheOptions, IEngineClient engineClient)
    {
        this.appCache = appCache;
        cacheSettings = cacheOptions.Value;
        this.engineClient = engineClient;
    }

    private class CachedEngineAvChannelResponse
    {
        public IReadOnlyCollection<string>? AvChannelPolicies;

        public CachedEngineAvChannelResponse(IReadOnlyCollection<string>? avChannelPolicies)
        {
            AvChannelPolicies = avChannelPolicies;
        }
    }

    public async Task<IReadOnlyCollection<string>?> RetrieveAvChannelPolicyOptions()
    {
        const string key = "avChannelPolicyOptions";

        var cachedResponse = await appCache.GetOrAddAsync(key, async entry =>
        {
            var avPolicyOptions = await engineClient.GetAllowedAvPolicyOptions();
            if (avPolicyOptions == null)
            {
                entry.AbsoluteExpirationRelativeToNow =
                    TimeSpan.FromSeconds(cacheSettings.GetTtl(CacheDuration.Short));
                return new CachedEngineAvChannelResponse(null);
            }
            
            return new CachedEngineAvChannelResponse(avPolicyOptions);
        }, cacheSettings.GetMemoryCacheOptions(CacheDuration.Long));

        return cachedResponse.AvChannelPolicies;
    }
}