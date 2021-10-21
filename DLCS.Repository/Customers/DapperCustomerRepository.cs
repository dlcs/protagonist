using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using DLCS.Model.Customers;
using DLCS.Model.PathElements;
using DLCS.Repository.Caching;
using LazyCache;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace DLCS.Repository.Customers
{
    public class DapperCustomerRepository : DapperRepository, ICustomerRepository
    {
        private readonly IAppCache appCache;
        private readonly CacheSettings cacheSettings;
        private static readonly Customer NullCustomer = new() { Id = -99 };

        public DapperCustomerRepository(
            IConfiguration configuration,
            IAppCache appCache,
            IOptions<CacheSettings> cacheSettings) : base(configuration)
        {
            this.appCache = appCache;
            this.cacheSettings = cacheSettings.Value;
        }

        public async Task<Dictionary<string, int>> GetCustomerIdLookup()
        {
            var results = await QueryAsync<CustomerPathElement>("SELECT \"Id\", \"Name\" FROM \"Customers\"");
            return results.ToDictionary(cpe => cpe.Name, cpe => cpe.Id);
        }
 
        public async Task<Customer?> GetCustomer(int customerId)
        {
            var customer = await GetCustomerInternal(customerId);
            return customer.Id == NullCustomer.Id ? null : customer;
        }

        public async Task<Customer?> GetCustomerForKey(string apiKey, int? customerIdHint)
        {
            if (customerIdHint.HasValue)
            {
                var customer = await GetCustomer(customerIdHint.Value);
                if (customer != null && customer.Keys.Contains(apiKey))
                {
                    // this key belongs to this customer
                    return customer; 
                }
            }
            // apiKey isn't associated with customerId. Is it an admin key?
            var admins = await GetAdminCustomers();
            return admins.SingleOrDefault(a => a.Keys.Contains(apiKey));
        }

        private Task<Customer> GetCustomerInternal(int customerId)
        {
            var key = $"cust:{customerId}";
            return appCache.GetOrAddAsync(key, async entry =>
            {
                await using var connection = await DatabaseConnectionManager.GetOpenNpgSqlConnection(configuration);
                dynamic? rawCustomer = await connection.QuerySingleOrDefaultAsync(CustomerSql, new { Id = customerId });
                if (rawCustomer == null)
                {
                    entry.AbsoluteExpirationRelativeToNow =
                        TimeSpan.FromSeconds(cacheSettings.GetTtl(CacheDuration.Short));
                    return NullCustomer;
                }

                // TODO: Why can't I replace this with return MapRawCustomer(rawCustomer) ?
                return new Customer
                {
                    Administrator = rawCustomer.Administrator,
                    Created = rawCustomer.Created,
                    Id = rawCustomer.Id,
                    Name = rawCustomer.Name,
                    AcceptedAgreement = rawCustomer.AcceptedAgreement,
                    DisplayName = rawCustomer.DisplayName,
                    Keys = rawCustomer.Keys.ToString().Split(',')
                };
            }, cacheSettings.GetMemoryCacheOptions());
        }

        private Task<List<Customer>> GetAdminCustomers()
        {
            const string key = "admin_customers";
            return appCache.GetOrAddAsync(key, async entry =>
            {
                // This allows for more than one admin customer - should it?
                var rawAdmins = await QueryAsync<dynamic>(AdminCustomersSql);
                var admins = new List<Customer>();
                foreach (dynamic rawCustomer in rawAdmins)
                {
                    admins.Add(MapRawCustomer(rawCustomer));
                }

                return admins;
            }, cacheSettings.GetMemoryCacheOptions());
        }

        private static Customer MapRawCustomer(dynamic? rawCustomer)
        {
            return new()
            {
                Administrator = rawCustomer.Administrator,
                Created = rawCustomer.Created,
                Id = rawCustomer.Id,
                Name = rawCustomer.Name,
                AcceptedAgreement = rawCustomer.AcceptedAgreement,
                DisplayName = rawCustomer.DisplayName,
                Keys = rawCustomer.Keys.ToString().Split(',')
            };
        }

        private const string CustomerSql = @"
SELECT ""Id"", ""Name"", ""DisplayName"", ""Keys"", ""Administrator"", ""Created"", ""AcceptedAgreement""
  FROM public.""Customers""
  WHERE ""Id""=@Id;";
        
        // TODO: make the shared SELECT list a const?
        private const string AdminCustomersSql = @"
SELECT ""Id"", ""Name"", ""DisplayName"", ""Keys"", ""Administrator"", ""Created"", ""AcceptedAgreement""
  FROM public.""Customers""
  WHERE ""Administrator""=True;";
    }
}
