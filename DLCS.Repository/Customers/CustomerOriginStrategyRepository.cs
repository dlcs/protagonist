using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DLCS.Model.Customer;
using DLCS.Repository.Settings;
using LazyCache;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DLCS.Repository.Customers
{
    public class CustomerOriginStrategyRepository : CustomerOriginStrategyBase
    {
        private readonly DlcsContext dbContext;

        public CustomerOriginStrategyRepository(
            DlcsContext dbContext,
            IAppCache appCache,
            IConfiguration configuration,
            IOptions<CacheSettings> cacheOptions,
            ILogger<CustomerOriginStrategyRepository> logger
            ) : base(appCache, configuration, cacheOptions, logger)
        {
            this.dbContext = dbContext;
        }

        protected override Task<List<CustomerOriginStrategy>> GetCustomerOriginStrategiesFromDb(int customer)
            => dbContext.CustomerOriginStrategies.AsNoTracking()
                .Where(cos => cos.Customer == customer)
                .ToListAsync();
    }
}