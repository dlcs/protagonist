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
    public class DapperCustomerRepository : ICustomerRepository
    {
        private readonly IConfiguration configuration;
        private readonly IAppCache appCache;
        private readonly CacheSettings cacheSettings;
        private static readonly Customer NullCustomer = new() { Id = -99 };

        public DapperCustomerRepository(
            IConfiguration configuration,
            IAppCache appCache,
            IOptions<CacheSettings> cacheSettings)
        {
            this.configuration = configuration;
            this.appCache = appCache;
            this.cacheSettings = cacheSettings.Value;
        }

        public async Task<Dictionary<string, int>> GetCustomerIdLookup()
        {
            await using var connection = await DatabaseConnectionManager.GetOpenNpgSqlConnection(configuration);
            var results = await connection.QueryAsync<CustomerPathElement>("SELECT \"Id\", \"Name\" FROM \"Customers\"");
            return results.ToDictionary(cpe => cpe.Name, cpe => cpe.Id);
        }
 
        public async Task<Customer?> GetCustomer(int customerId)
        {
            var customer = await GetCustomerInternal(customerId);
            return customer.Id == NullCustomer.Id ? null : customer;
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
        
        private const string CustomerSql = @"
SELECT ""Id"", ""Name"", ""DisplayName"", ""Keys"", ""Administrator"", ""Created"", ""AcceptedAgreement""
  FROM public.""Customers""
  WHERE ""Id""=@Id;";
    }
}
