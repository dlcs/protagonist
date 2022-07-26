using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using DLCS.Model.Assets.CustomHeaders;
using DLCS.Repository.Caching;
using LazyCache;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DLCS.Repository.Assets.CustomHeaders;

public class DapperCustomHeaderRepository : ICustomHeaderRepository
{
    private readonly IConfiguration configuration;
    private readonly IAppCache appCache;
    private readonly ILogger<DapperCustomHeaderRepository> logger;
    private readonly CacheSettings cacheSettings;
    
    public DapperCustomHeaderRepository(
        IConfiguration configuration,
        IAppCache appCache,
        IOptions<CacheSettings> cacheSettings,
        ILogger<DapperCustomHeaderRepository> logger)
    {
        this.configuration = configuration;
        this.appCache = appCache;
        this.logger = logger;
        this.cacheSettings = cacheSettings.Value;
    }
    
    public async Task<IEnumerable<CustomHeader>> GetForCustomer(int customerId)
    {
        var key = $"header:{customerId}";
        return await appCache.GetOrAddAsync(key, async () =>
        {
            logger.LogDebug("Refreshing {CacheKey} from database", key);
            await using var connection = await DatabaseConnectionManager.GetOpenNpgSqlConnection(configuration);
            var customHeaders =
                await connection.QueryAsync<CustomHeader>(CustomHeaderSql, new { Customer = customerId });
            return customHeaders ?? Enumerable.Empty<CustomHeader>();
        }, cacheSettings.GetMemoryCacheOptions(CacheDuration.Long, priority: CacheItemPriority.High));
    }
    
    private const string CustomHeaderSql = @"
SELECT ""Id"", ""Customer"", ""Space"", ""Role"", ""Key"", ""Value""
  FROM public.""CustomHeaders""
  WHERE ""Customer""=@customer;";
}