using System.Threading.Tasks;
using DLCS.Core.Caching;
using DLCS.Core.Collections;
using DLCS.Model.Customers;
using DLCS.Model.PathElements;
using LazyCache;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DLCS.Repository;

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

    public async Task<CustomerPathElement> GetCustomer(string customerPart)
    {
        // customerPart can be an int or a string name
        string customerName = null;
        if (int.TryParse(customerPart, out var customerId))
        {
            customerName = await GetCustomerName(customerId);
        }
        else
        {
            customerId = await GetCustomerId(customerPart);
            if (customerId > 0)
            {
                customerName = customerPart;
            }
        }
        return new CustomerPathElement(customerId, customerName);
    }

    private async Task<int> GetCustomerId(string customerName)
    {
        var readOnlyMap = await EnsureDictionary();
        return readOnlyMap.Forward[customerName];
    }

    private async Task<string> GetCustomerName(int customerId)
    {
        var readOnlyMap = await EnsureDictionary();
        return readOnlyMap.Reverse[customerId];
    }

    private Task<ReadOnlyMap<string, int>> EnsureDictionary()
    {
        const string key = "CustomerPathElementRepository_CustomerLookup";
        return appCache.GetOrAddAsync(key, async () =>
        {
            logger.LogDebug("Refreshing customer name/id lookup from database");
            return new ReadOnlyMap<string, int>(await customerRepository.GetCustomerIdLookup());
        }, cacheSettings.GetMemoryCacheOptions(CacheDuration.Long, priority: CacheItemPriority.High));
    }
}
