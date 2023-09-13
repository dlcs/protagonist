using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DLCS.Core.Caching;
using DLCS.Model.Customers;
using DLCS.Model.PathElements;
using LazyCache;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace DLCS.Repository.Customers;

public class DapperCustomerRepository : IDapperConfigRepository, ICustomerRepository
{
    public IConfiguration Configuration { get; }
    private readonly IAppCache appCache;
    private readonly CacheSettings cacheSettings;
    private static readonly Customer NullCustomer = new() { Id = -99 };

    public DapperCustomerRepository(
        IConfiguration configuration,
        IAppCache appCache,
        IOptions<CacheSettings> cacheSettings)
    {
        Configuration = configuration;
        this.appCache = appCache;
        this.cacheSettings = cacheSettings.Value;
    }

    public async Task<Dictionary<string, int>> GetCustomerIdLookup()
    {
        var results = await this.QueryAsync<CustomerPathElement>("SELECT \"Id\", \"Name\" FROM \"Customers\"");
        return results.ToDictionary(cpe => cpe.Name, cpe => cpe.Id);
    }

    public async Task<Customer?> GetCustomer(int customerId)
    {
        // Getting customer by Id uses appCache but by name does not.
        // Should this have an additional "bool preferCache = true" default?
        bool preferCache = true;
        if (preferCache)
        {
            var customer = await GetCustomerInternal(customerId);
            return customer.Id == NullCustomer.Id ? null : customer;
        }
        else
        {
            const string sql = CustomerSelect + @" WHERE ""Id""=@Id;";
            dynamic? rawCustomer = await this.QuerySingleOrDefaultAsync(sql, new {Id = customerId});
            return MapRawCustomer(rawCustomer);
        }
    }

    public async Task<Customer?> GetCustomer(string name)
    {
        const string sql = CustomerSelect + @" WHERE ""Name""=@name;";
        dynamic? rawCustomer = await this.QuerySingleOrDefaultAsync(sql, new {name});
        return MapRawCustomer(rawCustomer);
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
        var key = CacheKeys.Customer(customerId);
        return appCache.GetOrAddAsync(key, async entry =>
        {
            const string sql = CustomerSelect + @" WHERE ""Id""=@Id;";
            dynamic? rawCustomer = await this.QuerySingleOrDefaultAsync(sql, new {Id = customerId});
            if (rawCustomer == null)
            {
                entry.AbsoluteExpirationRelativeToNow =
                    TimeSpan.FromSeconds(cacheSettings.GetTtl(CacheDuration.Short));
                return NullCustomer;
            }

            Customer c = MapRawCustomer(rawCustomer);
            return c;
        }, cacheSettings.GetMemoryCacheOptions());
    }

    private Task<List<Customer>> GetAdminCustomers()
    {
        const string key = "admin_customers";
        return appCache.GetOrAddAsync(key, async entry =>
        {
            // This allows for more than one admin customer - should it?
            const string sql = CustomerSelect + @" WHERE ""Administrator""=True;";
            var rawAdmins = await this.QueryAsync(sql);
            var admins = new List<Customer>();
            foreach (dynamic rawCustomer in rawAdmins)
            {
                admins.Add(MapRawCustomer(rawCustomer));
            }

            return admins;
        }, cacheSettings.GetMemoryCacheOptions());
    }

    private static Customer? MapRawCustomer(dynamic? rawCustomer)
    {
        if (rawCustomer == null) return null;
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

    private const string CustomerSelect = @"
SELECT ""Id"", ""Name"", ""DisplayName"", ""Keys"", ""Administrator"", ""Created"", ""AcceptedAgreement""
  FROM public.""Customers"" ";
}
