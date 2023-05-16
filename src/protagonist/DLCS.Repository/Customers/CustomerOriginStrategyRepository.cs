using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DLCS.Core.Caching;
using DLCS.Model.Customers;
using LazyCache;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DLCS.Repository.Customers;

/// <summary>
/// Implementation of <see cref="ICustomerOriginStrategyRepository"/> using EF for data access 
/// </summary>
public class CustomerOriginStrategyRepository : CustomerOriginStrategyBase
{
    private readonly DlcsContext dbContext;

    public CustomerOriginStrategyRepository(
        IAppCache appCache,
        DlcsContext dbContext,
        IConfiguration configuration,
        IOptionsMonitor<CacheSettings> cacheOptions,
        ILogger<CustomerOriginStrategyRepository> logger
    ) : base(appCache, configuration, cacheOptions, logger)
    {
        this.dbContext = dbContext;
    }

    protected override Task<List<CustomerOriginStrategy>> GetCustomerOriginStrategiesFromDb(int customer)
        => dbContext.CustomerOriginStrategies.AsNoTracking()
            .Where(cos => cos.Customer == customer)
            .OrderBy(cos => cos.Order)
            .ToListAsync();

}