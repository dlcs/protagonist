using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DLCS.Model.Assets;
using DLCS.Repository.Caching;
using LazyCache;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DLCS.Repository.Assets
{
    public class ThumbnailPolicyRepository : DapperRepository, IThumbnailPolicyRepository
    {
        private readonly IAppCache appCache;
        private readonly CacheSettings cacheSettings;
        private readonly ILogger<ThumbnailPolicyRepository> logger;

        public ThumbnailPolicyRepository(IAppCache appCache,
            IConfiguration configuration,
            IOptions<CacheSettings> cacheOptions,
            ILogger<ThumbnailPolicyRepository> logger) : base(configuration)
        {
            this.appCache = appCache;
            this.logger = logger;
            cacheSettings = cacheOptions.Value;
        }
        
        public async Task<ThumbnailPolicy> GetThumbnailPolicy(string thumbnailPolicyId)
        {
            var thumbnailPolicies = await GetThumbnailPolicies();
            return thumbnailPolicies.SingleOrDefault(p => p.Id == thumbnailPolicyId);
        }

        private Task<List<ThumbnailPolicy>> GetThumbnailPolicies()
        {
            const string key = "ThumbRepository_ThumbnailPolicies";
            return appCache.GetOrAddAsync(key, async () =>
            {
                logger.LogInformation("refreshing ThumbnailPolicies from database");
                var thumbnailPolicies = await QueryAsync<ThumbnailPolicy>(
                    "SELECT \"Id\", \"Name\", \"Sizes\" FROM \"ThumbnailPolicies\"");
                return thumbnailPolicies.ToList();
            }, cacheSettings.GetMemoryCacheOptions());
        }
    }
}