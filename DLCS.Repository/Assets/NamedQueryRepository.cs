using System.Linq;
using System.Threading.Tasks;
using DLCS.Model.Assets;
using DLCS.Repository.Caching;
using LazyCache;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace DLCS.Repository.Assets
{
    public class NamedQueryRepository : INamedQueryRepository
    {
        private readonly DlcsContext dlcsContext;
        private readonly IAppCache appCache;
        private readonly CacheSettings cacheSettings;

        public NamedQueryRepository(
            DlcsContext dlcsContext,
            IAppCache appCache,
            IOptions<CacheSettings> cacheSettings)
        {
            this.dlcsContext = dlcsContext;
            this.appCache = appCache;
            this.cacheSettings = cacheSettings.Value;
        }
        
        public async Task<NamedQuery?> GetByName(int customer, string namedQueryName, bool includeGlobal = true)
        {
            var key = $"nq:{customer}:{namedQueryName}:{includeGlobal}";
            return await appCache.GetOrAddAsync(key, async () =>
            {
                var namedQueryQuery = dlcsContext.NamedQueries.Where(nq => nq.Name == namedQueryName);
                return includeGlobal
                    ? await namedQueryQuery.Where(nq => nq.Global || nq.Customer == customer)
                        .OrderBy(nq => nq.Global)
                        .FirstOrDefaultAsync()
                    : await namedQueryQuery.Where(nq => nq.Customer == customer)
                        .FirstOrDefaultAsync();
            }, cacheSettings.GetMemoryCacheOptions(CacheDuration.Short, priority: CacheItemPriority.Low));
        }
    }
}