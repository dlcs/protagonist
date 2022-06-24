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
    public class ImageOptimisationPolicyRepository : DapperRepository, IImageOptimisationPolicyRepository
    {        
        private readonly IAppCache appCache;
        private readonly CacheSettings cacheSettings;
        private readonly ILogger<ImageOptimisationPolicyRepository> logger;

        public ImageOptimisationPolicyRepository(
            IAppCache appCache,
            IConfiguration configuration,
            IOptions<CacheSettings> cacheOptions,
            ILogger<ImageOptimisationPolicyRepository> logger) : base(configuration)
        {
            this.appCache = appCache;
            this.logger = logger;
            cacheSettings = cacheOptions.Value;
        }
        
        public async Task<ImageOptimisationPolicy?> GetImageOptimisationPolicy(string id)
        {
            var imageOptimisationPolicies = await GetImageOptimisationPolicies();
            return imageOptimisationPolicies.SingleOrDefault(p => p.Id == id);
        }

        private Task<List<ImageOptimisationPolicy>> GetImageOptimisationPolicies()
        {
            const string key = "ImageOptimisationPolicyRepository_Policies";
            return appCache.GetOrAddAsync(key, async () =>
            {
                logger.LogInformation("refreshing ImageOptimisationPolicies from database");
                var imageOptimisationPolicies = await QueryAsync<ImageOptimisationPolicy>(
                    "SELECT \"Id\", \"Name\", \"TechnicalDetails\" FROM \"ImageOptimisationPolicies\"");
                return imageOptimisationPolicies.ToList();
            }, cacheSettings.GetMemoryCacheOptions());
        }
    }
}