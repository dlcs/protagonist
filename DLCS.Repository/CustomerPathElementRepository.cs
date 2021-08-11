using System;
using System.Threading.Tasks;
using DLCS.Model.Customer;
using DLCS.Model.PathElements;
using DLCS.Repository.Collections;
using LazyCache;
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
            return appCache.GetOrAddAsync(key, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                logger.LogDebug("refreshing customer name/id lookup from database");
                return new ReadOnlyMap<string, int>(await customerRepository.GetCustomerIdLookup());
            });
        }
    }
}
