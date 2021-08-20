using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using DLCS.Model.Customer;
using DLCS.Repository.Settings;
using LazyCache;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DLCS.Repository.Customers
{
    public class DapperCustomerOriginStrategyRepository : CustomerOriginStrategyBase
    {
        private readonly IConfiguration configuration;

        public DapperCustomerOriginStrategyRepository(
            IAppCache appCache,
            IConfiguration configuration,
            IOptions<CacheSettings> cacheOptions,
            ILogger<CustomerOriginStrategyRepository> logger
        ) : base(appCache, configuration, cacheOptions, logger)
        {
            this.configuration = configuration;
        }

        protected override async Task<List<CustomerOriginStrategy>> GetCustomerOriginStrategiesFromDb(int customer)
        {
            await using var connection = await DatabaseConnectionManager.GetOpenNpgSqlConnection(configuration);
            var result = await connection.QueryAsync<CustomerOriginStrategy>(CustomerOriginStrategySql,
                new { Customer = customer });
            return result.ToList();
        }

        private const string CustomerOriginStrategySql = @"
SELECT ""Id"", ""Customer"", ""Regex"", ""Strategy"", ""Credentials"", ""Optimised"" 
FROM ""CustomerOriginStrategies""
WHERE ""Customer""=@Customer;
";
    }
}