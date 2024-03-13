using System.Collections.Generic;
using DLCS.Core.Caching;
using DLCS.Model.DeliveryChannels;
using DLCS.Repository.Messaging;
using LazyCache;
using Microsoft.Extensions.Options;

namespace API.Features.DeliveryChannels;

public class AvPolicyOptionsRepository : IAvPolicyOptionsRepository
{
    private readonly IAppCache appCache;
    private readonly CacheSettings cacheSettings;
    private readonly IEngineClient engineClient;

    public AvPolicyOptionsRepository(IAppCache appCache, IOptions<CacheSettings> cacheOptions, IEngineClient engineClient)
    {
        this.appCache = appCache;
        cacheSettings = cacheOptions.Value;
        this.engineClient = engineClient;
    }

    public async Task<IReadOnlyCollection<string>> RetrieveAvChannelPolicyOptions()
    {
        const string key = "avChannelPolicyOptions";
        
        return await appCache.GetOrAdd(key, () => engineClient.GetAllowedAvOptions(),
            cacheSettings.GetMemoryCacheOptions(CacheDuration.Long)); 
    }
}