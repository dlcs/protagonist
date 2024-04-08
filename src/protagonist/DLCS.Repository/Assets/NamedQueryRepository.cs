using System;
using System.Linq;
using System.Threading.Tasks;
using DLCS.Core.Caching;
using DLCS.Core.Strings;
using DLCS.Model.Assets;
using DLCS.Model.Assets.NamedQueries;
using LazyCache;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace DLCS.Repository.Assets;

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
            try
            {
                var namedQueryQuery = dlcsContext.NamedQueries.Where(nq => nq.Name == namedQueryName);
                return includeGlobal
                    ? await namedQueryQuery.Where(nq => nq.Global || nq.Customer == customer)
                        .OrderBy(nq => nq.Global)
                        .FirstOrDefaultAsync()
                    : await namedQueryQuery.Where(nq => nq.Customer == customer)
                        .FirstOrDefaultAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }, cacheSettings.GetMemoryCacheOptions(CacheDuration.Short, priority: CacheItemPriority.Low));
    }

    public IQueryable<Asset> GetNamedQueryResults(ParsedNamedQuery query)
    {
        var imageFilter = dlcsContext.Images.Include(i => i.ImageDeliveryChannels)
            .Where(i => i.Customer == query.Customer && !i.NotForDelivery);

        if (query.String1.HasText())
        {
            imageFilter = imageFilter.Where(i => i.Reference1 == query.String1);
        }

        if (query.String2.HasText())
        {
            imageFilter = imageFilter.Where(i => i.Reference2 == query.String2);
        }

        if (query.String3.HasText())
        {
            imageFilter = imageFilter.Where(i => i.Reference3 == query.String3);
        }

        if (query.Number1.HasValue)
        {
            imageFilter = imageFilter.Where(i => i.NumberReference1 == query.Number1);
        }

        if (query.Number2.HasValue)
        {
            imageFilter = imageFilter.Where(i => i.NumberReference2 == query.Number2);
        }

        if (query.Number3.HasValue)
        {
            imageFilter = imageFilter.Where(i => i.NumberReference3 == query.Number3);
        }

        if (query.Space.HasValue)
        {
            imageFilter = imageFilter.Where(i => i.Space == query.Space);
        }
        else if (query.SpaceName.HasText())
        {
            imageFilter = imageFilter.Join(dlcsContext.Spaces, asset => asset.Space, space => space.Id,
                    (asset, space) => new
                    {
                        Image = asset,
                        SpaceName = space.Name
                    })
                .Where(arg => arg.SpaceName == query.SpaceName)
                .Select(arg => arg.Image);
        }

        return imageFilter;
    }
}