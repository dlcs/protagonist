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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DLCS.Repository.Customers;

/// <summary>
/// Base class that manages finding correct customer origin strategy for specified origin
/// </summary>
public abstract class CustomerOriginStrategyBase : ICustomerOriginStrategyRepository
{
    private const string OriginRegexAppSettings = "S3OriginRegex";

    private static readonly CustomerOriginStrategy DefaultStrategy = new()
        { Id = "_default_", Strategy = OriginStrategyType.Default };
    
    private readonly IAppCache appCache;
    private readonly IOptionsMonitor<CacheSettings> cacheSettings;
    private readonly string s3OriginRegex;
    private readonly ILogger logger;

    protected CustomerOriginStrategyBase(
        IAppCache appCache,
        IConfiguration configuration,
        IOptionsMonitor<CacheSettings> cacheOptions,
        ILogger logger
    )
    {
        this.appCache = appCache;
        this.logger = logger;
        cacheSettings = cacheOptions;

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
        var matching = FindMatchingStrategy(asset.Origin, customerStrategies) ?? DefaultStrategy;
        
        logger.LogTrace("Using strategy: {Strategy} ('{StrategyId}') for handling asset '{AssetId}'",
            matching.Strategy, matching.Id, asset.Id);
        
        return matching;
    }
    
    protected abstract Task<List<CustomerOriginStrategy>> GetCustomerOriginStrategiesFromDb(int customer);

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
        }, cacheSettings.CurrentValue.GetMemoryCacheOptions());
    }

    // NOTE(DG): This CustomerOriginStrategy is for assets uploaded directly via the portal
    private CustomerOriginStrategy GetPortalOriginStrategy(int customer) 
        => new()
        {
            Customer = customer,
            Id = "_default_portal_",
            Regex = s3OriginRegex,
            Strategy = OriginStrategyType.S3Ambient,
            Order = 999,
            Optimised = true,
        };

    private static CustomerOriginStrategy? FindMatchingStrategy(
        string origin,
        IEnumerable<CustomerOriginStrategy> customerStrategies)
        => customerStrategies.FirstOrDefault(cos =>
            Regex.IsMatch(origin, cos.Regex, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
}