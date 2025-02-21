using System;
using System.Collections.Generic;
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
/// Internally this is a read-through cache and has multiple cached lookups values. Customers are added as they are
/// requested. This can result in slightly more requests to DB than <see cref="BulkCustomerPathElementRepository"/> but
/// reduces need to invalidate when new customers are created. 
/// </remarks>
public class GranularCustomerPathElementRepository : CustomerPathElementTemplate
{
    private readonly ICustomerRepository customerRepository;
    private readonly ILogger<GranularCustomerPathElementRepository> logger;
    private readonly CacheSettings cacheSettings;
    private readonly IAppCache appCache;

    public GranularCustomerPathElementRepository(
        IAppCache appCache,
        IOptions<CacheSettings> cacheOptions,
        ICustomerRepository customerRepository,
        ILogger<GranularCustomerPathElementRepository> logger)
    {
        this.customerRepository = customerRepository;
        this.logger = logger;
        this.appCache = appCache;
        cacheSettings = cacheOptions.Value;
    }
    
    protected override Task<int> GetCustomerId(string customerName)
        => GetCustomerLookup<string, int>(customerName,
            CacheKeys.CustomerByName(customerName),
            -99,
            s => customerRepository.GetCustomer(s),
            customer => customer.Id);

    protected override Task<string> GetCustomerName(int customerId)
        => GetCustomerLookup<int, string>(customerId,
            CacheKeys.CustomerById(customerId),
            "_nocust_",
            i => customerRepository.GetCustomer(i),
            customer => customer.Name);
    
    private async Task<TOut> GetCustomerLookup<TIn, TOut>(TIn customerLookup, string cacheKey, TOut nullValue,
        Func<TIn, Task<Customer?>> query, Func<Customer, TOut> propertyFinder)
    {
        var customerProp = await appCache.GetOrAddAsync(cacheKey, async entry =>
        {
            logger.LogDebug("Refreshing customer lookup {CacheKey} from database", cacheKey);
            var customer = await query(customerLookup);
            if (customer == null)
            {
                entry.SetAbsoluteExpiration(TimeSpan.FromSeconds(cacheSettings.GetTtl(CacheDuration.Short)));
                return nullValue;
            }
            return propertyFinder(customer);
        }, cacheSettings.GetMemoryCacheOptions(CacheDuration.Long, priority: CacheItemPriority.High));

        return customerProp == null || customerProp.Equals(nullValue)
            ? throw new KeyNotFoundException($"Customer {customerLookup} not found")
            : customerProp;
    }
}