using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DLCS.Core.Caching;
using DLCS.Model.Customers;
using DLCS.Model.PathElements;
using LazyCache;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DLCS.Repository;

/// <summary>
/// Class that manages looking up CustomerName to get Id, or vice versa.
/// </summary>
/// <remarks>
/// Internally this has 2 cached lookups values:
///  Lowercase-Name:Id - this allows us to lookup "Customer", "customer", "CUSTOMER" to get Id
///  Id:Name - this allows us to use Id to get name as saved in DB 
/// </remarks>
public class CustomerPathElementRepository : IPathCustomerRepository
{
    private readonly ICustomerRepository customerRepository;
    private readonly ILogger<CustomerPathElementRepository> logger;
    private readonly CacheSettings cacheSettings;
    private readonly IAppCache appCache;

    public CustomerPathElementRepository(
        IAppCache appCache,
        IOptions<CacheSettings> cacheOptions,
        ICustomerRepository customerRepository,
        ILogger<CustomerPathElementRepository> logger)
    {
        this.customerRepository = customerRepository;
        this.logger = logger;
        this.appCache = appCache;
        cacheSettings = cacheOptions.Value;
    }

    public async Task<CustomerPathElement> GetCustomerPathElement(string customerPart)
    {
        // customerPart can be an int or a string name
        if (int.TryParse(customerPart, out var customerId))
        {
            var customerName = await GetCustomerName(customerId);
            return new CustomerPathElement(customerId, customerName);
        }
        else
        {
            customerId = await GetCustomerId(customerPart);
            return new CustomerPathElement(customerId, customerPart);
        }
    }

    private async Task<int> GetCustomerId(string customerName)
    {
        var idLookup = await GetIdLookup();
        return idLookup[customerName.ToLower()];
    }

    private async Task<string> GetCustomerName(int customerId)
    {
        var nameLookup = await GetNameLookup();
        return nameLookup[customerId];
    }

    private Task<Dictionary<string, int>> GetIdLookup()
        => GetLookupResult(
            "CustomerIdLookup",
            customerIdLookup => customerIdLookup.ToDictionary(kvp => kvp.Key.ToLower(), kvp => kvp.Value));

    private Task<Dictionary<int, string>> GetNameLookup()
        => GetLookupResult(
            "CustomerNameLookup",
            customerIdLookup => customerIdLookup.ToDictionary(kvp => kvp.Value, kvp => kvp.Key));

    /// <summary>
    /// Id:Name and Name:Id lookup are the same data reversed. This method takes a transform function to convert from
    /// Name:Id to relevant shape to cache 
    /// </summary>
    /// <param name="cacheKey"></param>
    /// <param name="transformer"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns>Transformed object</returns>
    private Task<T> GetLookupResult<T>(string cacheKey, Func<Dictionary<string, int>, T> transformer)
    {
        return appCache.GetOrAddAsync(cacheKey, async () =>
        {
            logger.LogDebug("Refreshing customer lookup {CacheKey} from database", cacheKey);
            var customerIdLookup = await customerRepository.GetCustomerIdLookup();
            return transformer(customerIdLookup);
        }, cacheSettings.GetMemoryCacheOptions(CacheDuration.Long, priority: CacheItemPriority.High));
    }
}
