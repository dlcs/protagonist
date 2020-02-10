using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DLCS.Model.Customer;
using DLCS.Model.PathElements;
using LazyCache;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace DLCS.Repository
{
    public class CustomerPathElementRepository : IPathCustomerRepository
    {
        private readonly ICustomerRepository customerRepository;
        private readonly ILogger<CustomerPathElementRepository> logger;
        private readonly IAppCache appCache;

        public CustomerPathElementRepository(
            IAppCache appCache,
            ICustomerRepository customerRepository,
            ILogger<CustomerPathElementRepository> logger)
        {
            this.customerRepository = customerRepository;
            this.logger = logger;
            this.appCache = appCache;
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
            return new CustomerPathElement {Id = customerId, Name = customerName};
        }

        private async Task<int> GetCustomerId(string customerName)
        {
            Dictionary<string, int> lookup = await EnsureDictionary();
            return lookup[customerName];
        }

        private async Task<string> GetCustomerName(int customerId)
        {
            Dictionary<int, string> lookup = await EnsureInverseDictionary();
            return lookup[customerId];
        }

        private Task<Dictionary<string, int>> EnsureDictionary()
        {
            // TODO: Investigate locks, best caching approach, etc, LazyCache
            const string key = "CustomerPathElementRepository_CustomerNameLookupKey";
            return appCache.GetOrAddAsync(key, entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
                    logger.LogInformation("refreshing customer name => id lookup from database");
                    return customerRepository.GetCustomerIdLookup();
                });
        }

        private Task<Dictionary<int, string>> EnsureInverseDictionary()
        {
            const string key = "CustomerPathElementRepository_InverseCustomerNameLookupKey";
            return appCache.GetOrAddAsync(key, async entry => 
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
                logger.LogInformation("refreshing customer id => name lookup from database");
                var customerIdLookup = await customerRepository.GetCustomerIdLookup();
                return customerIdLookup.ToDictionary(
                    kvp => kvp.Value, kvp => kvp.Key);
            });
        }
    }
}
