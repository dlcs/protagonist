using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DLCS.Core.Caching;
using DLCS.Core.Enum;
using DLCS.Model.Customers;
using LazyCache;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DLCS.Repository.Customers;

public class DapperCustomerOriginStrategyRepository : CustomerOriginStrategyBase, IDapperConfigRepository
{
    public IConfiguration Configuration { get; }
    
    public DapperCustomerOriginStrategyRepository(
        IAppCache appCache,
        IConfiguration configuration,
        IOptionsMonitor<CacheSettings> cacheOptions,
        ILogger<DapperCustomerOriginStrategyRepository> logger) : base(appCache, configuration, cacheOptions,
        logger)
    {
        Configuration = configuration;
    }

    protected override async Task<List<CustomerOriginStrategy>> GetCustomerOriginStrategiesFromDb(int customer)
    {
        const string query =
            "SELECT \"Id\", \"Customer\", \"Regex\", \"Strategy\", \"Credentials\", \"Optimised\", \"Order\" FROM \"CustomerOriginStrategies\" WHERE \"Customer\" = @customer;";

        //var rawStrategies = await this.QueryAsync(query, new { Customer = customer });
        var rawStrategies = (await this.QueryAsync(query, new { Customer = customer })).ToList();
        var strategies = new List<CustomerOriginStrategy>(rawStrategies.Count);
        foreach (dynamic s in rawStrategies)
        {
            strategies.Add(MapCustomerOriginStrategy(s));
        }
        
        return strategies;
    }

    private static CustomerOriginStrategy MapCustomerOriginStrategy(dynamic cos)
    {
        string strategy = cos.Strategy.ToString();
        return new CustomerOriginStrategy
        {
            Id = cos.Id,
            Customer = cos.Customer,
            Regex = cos.Regex,
            Strategy = strategy.GetEnumFromString<OriginStrategyType>(true),
            Optimised = cos.Optimised,
            Order = cos.Order,
            Credentials = cos.Credentials
        };
    }
}
