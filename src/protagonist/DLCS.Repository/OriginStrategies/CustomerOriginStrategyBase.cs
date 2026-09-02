using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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

namespace DLCS.Repository.OriginStrategies;

/// <summary>
/// Base class that manages finding correct customer origin strategy for specified origin
/// </summary>
public abstract class CustomerOriginStrategyBase : ICustomerOriginStrategyRepository
{
    private const string OriginRegexAppSettings = "S3OriginRegex";

    // Cache a max number of regexes. Beyond limit regexes are built per-match, which is slower but still correct
    private const int MaxCachedRegex = 1000;

    private static readonly CustomerOriginStrategy DefaultStrategy = new()
        { Id = "_default_", Strategy = OriginStrategyType.Default };

    private static readonly ConcurrentDictionary<RegexCacheKey, Regex> RegexCache = new();

    private readonly IAppCache appCache;
    private readonly IOptionsMonitor<CacheSettings> cacheSettings;
    private readonly string s3OriginRegex;
    private readonly OriginStrategySettings settings;
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
        settings = OriginStrategySettings.FromConfiguration(configuration);
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

    public Task<CustomerOriginStrategy> GetCustomerOriginStrategy(Asset asset, bool initialIngestion = false)
        => GetCustomerOriginStrategy(asset.Customer, asset);

    public Task<CustomerOriginStrategy> GetCustomerOriginStrategy(Adjunct adjunct)
        => GetCustomerOriginStrategy(adjunct.Asset.Customer, adjunct);

    private async Task<CustomerOriginStrategy> GetCustomerOriginStrategy(int customerId, IDeliverable deliverable)
    {
        // Ones without origin would not have been sent for ingestion, this is part of the API processing
        Debug.Assert(deliverable.Origin != null,  nameof(deliverable.Origin) + " != null");
        
        var customerStrategies = await GetCustomerOriginStrategies(customerId);
        var matching = FindMatchingStrategy(deliverable.Origin, customerStrategies) ?? DefaultStrategy;
        
        logger.LogTrace("Using strategy: {Strategy} ('{StrategyId}') for handling {Deliverable}",
            matching.Strategy, matching.Id, deliverable.Identifier());
        
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

    private CustomerOriginStrategy? FindMatchingStrategy(
        string origin,
        IEnumerable<CustomerOriginStrategy> customerStrategies)
        => customerStrategies.FirstOrDefault(cos => IsMatchingStrategy(origin, cos));

    private bool IsMatchingStrategy(string origin, CustomerOriginStrategy strategy)
    {
        try
        {
            return GetRegex(strategy).IsMatch(origin);
        }
        catch (ArgumentException ex)
        {
            // Pattern isn't a valid regex. Predates validation, or was written directly to the database
            logger.LogError(ex,
                "Origin strategy '{StrategyId}' for customer {Customer} has an invalid regex, unable to match " +
                "origin {Origin}", strategy.Id, strategy.Customer, origin);
            throw new OriginStrategyRegexException(strategy, "is not a valid regular expression", ex);
        }
        catch (RegexMatchTimeoutException ex)
        {
            logger.LogError(ex,
                "Origin strategy '{StrategyId}' for customer {Customer} timed out after {Timeout} matching " +
                "origin {Origin}", strategy.Id, strategy.Customer, settings.MatchTimeout, origin);
            throw new OriginStrategyRegexException(strategy, $"timed out after {settings.MatchTimeout}", ex);
        }
    }

    private Regex GetRegex(CustomerOriginStrategy strategy)
    {
        var key = new RegexCacheKey(strategy.Regex, settings.UseNonBacktracking, settings.MatchTimeout);
        if (RegexCache.TryGetValue(key, out var cached)) return cached;

        logger.LogTrace("Creating regex '{Regex}'..", strategy.Regex);
        var regex = OriginStrategyRegex.Create(strategy.Regex, settings, out var nonBacktracking);

        if (settings.UseNonBacktracking && !nonBacktracking)
        {
            logger.LogWarning(
                "Origin strategy '{StrategyId}' for customer {Customer} uses a regex that can't be evaluated " +
                "without backtracking, falling back to a {Timeout} match timeout",
                strategy.Id, strategy.Customer, settings.MatchTimeout);
        }

        if (RegexCache.Count < MaxCachedRegex)
        {
            logger.LogTrace("Adding regex {Regex} to cache", strategy.Regex);
            RegexCache.TryAdd(key, regex);
        }

        return regex;
    }

    private readonly record struct RegexCacheKey(string Pattern, bool NonBacktracking, TimeSpan MatchTimeout);
}
