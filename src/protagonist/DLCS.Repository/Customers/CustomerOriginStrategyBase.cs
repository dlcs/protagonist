using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DLCS.Core.Caching;
using DLCS.Core.Guard;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Customers;
using LazyCache;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DLCS.Repository.Customers;

public class CustomerOriginStrategyRepository : ICustomerOriginStrategyRepository
{
    private const string OriginRegexAppSettings = "S3OriginRegex";

    private static readonly CustomerOriginStrategy DefaultStrategy = new()
        { Id = "_default_", Strategy = OriginStrategyType.Default };
    
    private readonly IAppCache appCache;
    private readonly DlcsContext dbContext;
    private readonly CacheSettings cacheSettings;
    private readonly string s3OriginRegex;
    private readonly ILogger<CustomerOriginStrategyRepository> logger;

    public CustomerOriginStrategyRepository(
        IAppCache appCache,
        DlcsContext dbContext,
        IConfiguration configuration,
        IOptions<CacheSettings> cacheOptions,
        ILogger<CustomerOriginStrategyRepository> logger
    )
    {
        this.appCache = appCache;
        this.dbContext = dbContext;
        this.logger = logger;
        cacheSettings = cacheOptions.Value;

        s3OriginRegex = configuration[OriginRegexAppSettings]
            .ThrowIfNullOrWhiteSpace($"appsetting:{OriginRegexAppSettings}");
    }

    public Task<IEnumerable<CustomerOriginStrategy>> GetCustomerOriginStrategies(int customer)
        => GetStrategiesForCustomer(customer);

    public async Task<CustomerOriginStrategy> GetCustomerOriginStrategy(AssetId assetId, string origin)
    {
        var customerStrategies = await GetCustomerOriginStrategies(assetId.Customer);
        
        var matching = FindMatchingStrategy(origin, customerStrategies) ?? DefaultStrategy;
        logger.LogTrace("Using strategy: {Strategy} ('{StrategyId}') for handling asset '{AssetId}'",
            matching.Strategy, matching.Id, assetId);
        
        return matching;
    }

    public async Task<CustomerOriginStrategy> GetCustomerOriginStrategy(Asset asset, bool initialIngestion = false)
    {
        var customerStrategies = await GetCustomerOriginStrategies(asset.Customer);
        var assetOrigin = initialIngestion ? asset.GetIngestOrigin() : asset.Origin;
        var matching = FindMatchingStrategy(assetOrigin, customerStrategies) ?? DefaultStrategy;
        
        logger.LogTrace("Using strategy: {Strategy} ('{StrategyId}') for handling asset '{AssetId}'",
            matching.Strategy, matching.Id, asset.Id);
        
        return matching;
    }

    private async Task<IEnumerable<CustomerOriginStrategy>> GetStrategiesForCustomer(int customer)
    {
        var key = $"OriginStrategy:{customer}";
        return await appCache.GetOrAddAsync(key, async () =>
        {
            logger.LogDebug("Refreshing CustomerOriginStrategy from database for customer {Customer}",
                customer);

            var origins = await GetCustomerOriginStrategiesFromDb(customer);
            origins.Add(GetPortalOriginStrategy(customer));
            return origins;
        }, cacheSettings.GetMemoryCacheOptions());
    }

    private Task<List<CustomerOriginStrategy>> GetCustomerOriginStrategiesFromDb(int customer)
        => dbContext.CustomerOriginStrategies.AsNoTracking()
            .Where(cos => cos.Customer == customer)
            .ToListAsync();

    // NOTE(DG): This CustomerOriginStrategy is for assets uploaded directly via the portal
    private CustomerOriginStrategy GetPortalOriginStrategy(int customer) 
        => new()
        {
            Customer = customer,
            Id = "_default_portal_",
            Regex = s3OriginRegex,
            Strategy = OriginStrategyType.S3Ambient
        };

    private static CustomerOriginStrategy? FindMatchingStrategy(
        string origin,
        IEnumerable<CustomerOriginStrategy> customerStrategies)
        => customerStrategies.FirstOrDefault(cos =>
            Regex.IsMatch(origin, cos.Regex, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
}