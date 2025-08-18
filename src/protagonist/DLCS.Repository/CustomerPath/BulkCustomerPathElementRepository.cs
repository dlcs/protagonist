using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DLCS.Core.Caching;
using DLCS.Model.Customers;
using LazyCache;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DLCS.Repository.CustomerPath;

/// <summary>
/// Class that manages looking up CustomerName to get Id, or vice versa.
/// </summary>
/// <remarks>
/// Internally this has 2 cached lookups values:
///  Lowercase-Name:Id - this allows us to lookup "Customer", "customer", "CUSTOMER" to get Id
///  Id:Name - this allows us to use Id to get name as saved in DB 
/// </remarks>
public class BulkCustomerPathElementRepository : CustomerPathElementTemplate
{
    private readonly ICustomerRepository customerRepository;
    private readonly ILogger<BulkCustomerPathElementRepository> logger;
    private readonly CacheSettings cacheSettings;
    private readonly IAppCache appCache;

    public BulkCustomerPathElementRepository(
        IAppCache appCache,
        IOptions<CacheSettings> cacheOptions,
        ICustomerRepository customerRepository,
        ILogger<BulkCustomerPathElementRepository> logger)
    {
        this.customerRepository = customerRepository;
        this.logger = logger;
        this.appCache = appCache;
        cacheSettings = cacheOptions.Value;
    }
    
    protected override async Task<int> GetCustomerId(string customerName)
    {
        var idLookup = await GetIdLookup();
        return idLookup[customerName.ToLower()];
    }

    protected override async Task<string> GetCustomerName(int customerId)
    {
        var nameLookup = await GetNameLookup();
        return nameLookup[customerId];
    }

    private Task<Dictionary<string, int>> GetIdLookup()
        => GetLookupResult(
            CacheKeys.CustomerIdLookup,
            customerIdLookup => customerIdLookup.ToDictionary(kvp => kvp.Key.ToLower(), kvp => kvp.Value));

    private Task<Dictionary<int, string>> GetNameLookup()
        => GetLookupResult(
            CacheKeys.CustomerNameLookup,
            customerIdLookup => customerIdLookup.ToDictionary(kvp => kvp.Value, kvp => kvp.Key));

    /// <summary>
    /// Id:Name and Name:Id lookup are the same data reversed. This method takes a transform function to convert from
    /// Name:Id to relevant shape to cache 
    /// </summary>
    /// <returns>Transformed object</returns>
    private Task<T> GetLookupResult<T>(string cacheKey, Func<Dictionary<string, int>, T> transformer) =>
        appCache.GetOrAddAsync(cacheKey, async () =>
        {
            logger.LogDebug("Refreshing customer lookup {CacheKey} from database", cacheKey);
            var customerIdLookup = await customerRepository.GetCustomerIdLookup();
            return transformer(customerIdLookup);
        }, cacheSettings.GetMemoryCacheOptions(CacheDuration.Long, priority: CacheItemPriority.High));
}