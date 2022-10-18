using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Caching;
using DLCS.Model.Policies;
using LazyCache;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DLCS.Repository.Policies;

public class DapperThumbnailPolicy : IDapperConfigRepository, IThumbnailPolicyRepository
{
    public IConfiguration Configuration { get; }
    private readonly IAppCache appCache;
    private readonly CacheSettings cacheSettings;
    private readonly ILogger<DapperThumbnailPolicy> logger;
        
    public DapperThumbnailPolicy(
        IAppCache appCache,
        IConfiguration configuration,
        IOptions<CacheSettings> cacheOptions,
        ILogger<DapperThumbnailPolicy> logger)
    {
        this.appCache = appCache;
        this.logger = logger;
        cacheSettings = cacheOptions.Value;
        Configuration = configuration;
    }

    public async Task<ThumbnailPolicy?> GetThumbnailPolicy(string thumbnailPolicyId,
        CancellationToken cancellationToken = default)
    {
        var thumbnailPolicies = await GetThumbnailPolicies();
        return thumbnailPolicies.SingleOrDefault(p => p.Id == thumbnailPolicyId);
    }

    private Task<List<ThumbnailPolicy>> GetThumbnailPolicies()
    {
        const string key = "ThumbnailPolicies";
        return appCache.GetOrAddAsync(key, async () =>
        {
            logger.LogDebug("Refreshing ThumbnailPolicies from database");
            var thumbnailPolicies = await this.QueryAsync<ThumbnailPolicy>(
                "SELECT \"Id\", \"Name\", \"Sizes\" FROM \"ThumbnailPolicies\"");
            return thumbnailPolicies.ToList();
        }, cacheSettings.GetMemoryCacheOptions());
    }
}