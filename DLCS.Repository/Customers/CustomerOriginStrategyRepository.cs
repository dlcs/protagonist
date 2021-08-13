using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DLCS.Core.Guard;
using DLCS.Model.Assets;
using DLCS.Model.Customer;
using LazyCache;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DLCS.Repository.Customers
{
    public class CustomerOriginStrategyRepository : ICustomerOriginStrategyRepository
    {
        private const string OriginRegexAppSettings = "s3OriginRegex";

        private static readonly CustomerOriginStrategy DefaultStrategy = new()
            { Id = "_default_", Strategy = OriginStrategyType.Default };
        
        private readonly DlcsContext dbContext;
        private readonly IAppCache appCache;
        private readonly string s3OriginRegex;
        private readonly ILogger<CustomerOriginStrategyRepository> logger;

        public CustomerOriginStrategyRepository(
            DlcsContext dbContext,
            IAppCache appCache,
            IConfiguration configuration,
            ILogger<CustomerOriginStrategyRepository> logger
            )
        {
            this.dbContext = dbContext;
            this.appCache = appCache;
            this.logger = logger;

            s3OriginRegex = configuration[OriginRegexAppSettings]
                .ThrowIfNullOrWhiteSpace($"appsetting:{OriginRegexAppSettings}");
        }

        public Task<IEnumerable<CustomerOriginStrategy>> GetCustomerOriginStrategies(int customer)
            => GetStrategiesForCustomer(customer);

        public async Task<CustomerOriginStrategy> GetCustomerOriginStrategy(Asset asset, bool initialIngestion = false)
        {
            asset.ThrowIfNull(nameof(asset));
            
            var customerStrategies = await GetCustomerOriginStrategies(asset.Customer);

            var assetOrigin = initialIngestion ? asset.GetIngestOrigin() : asset.Origin;
            var matching = FindMatchingStrategy(assetOrigin, customerStrategies) ?? DefaultStrategy;
            logger.LogTrace("Using strategy: {strategy} ('{strategyId}') for handling asset '{assetId}'",
                matching.Strategy, matching.Id, asset.Id);
            
            return matching;
        }
        
        private async Task<IEnumerable<CustomerOriginStrategy>> GetStrategiesForCustomer(int customer)
        {
            var key = $"OriginStrategy:{customer}";
            return await appCache.GetOrAddAsync(key, async entry =>
            {
                // TODO - config
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                logger.LogInformation("Refreshing CustomerOriginStrategy from database for customer {customer}",
                    customer);

                var origins = dbContext.CustomerOriginStrategies.AsNoTracking().Where(cos => cos.Customer == customer)
                    .ToList();
                origins.Add(GetPortalOriginStrategy(customer));
                return origins;
            });
        }
        
        // NOTE(DG): This CustomerOriginStrategy is for assets uploaded directly via the portal
        private CustomerOriginStrategy GetPortalOriginStrategy(int customer) 
            => new()
            {
                Customer = customer,
                Id = "_default_portal_",
                Regex = s3OriginRegex,
                Strategy = OriginStrategyType.S3Ambient
            };

        private static CustomerOriginStrategy? FindMatchingStrategy(
            string origin,
            IEnumerable<CustomerOriginStrategy> customerStrategies)
            => customerStrategies.FirstOrDefault(cos =>
                Regex.IsMatch(origin, cos.Regex, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
    }
}