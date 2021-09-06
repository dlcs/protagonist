using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Types;
using DLCS.Model.Customers;
using DLCS.Repository.Strategy.DependencyInjection;

namespace DLCS.Repository.Strategy.Utils
{
    public class OriginFetcher
    {
        private readonly ICustomerOriginStrategyRepository customerOriginStrategyRepository;
        private readonly OriginStrategyResolver originStrategyResolver;

        public OriginFetcher(
            ICustomerOriginStrategyRepository customerOriginStrategyRepository,
            OriginStrategyResolver originStrategyResolver
        )
        {
            this.customerOriginStrategyRepository = customerOriginStrategyRepository;
            this.originStrategyResolver = originStrategyResolver;
        }

        /// <summary>
        /// Get <see cref="OriginResponse"/> object for provided asset, loading from origin using relevant origin
        /// strategy for customer
        /// </summary>
        /// <param name="assetId">Asset to load</param>
        /// <param name="location">Location to load asset from</param>
        /// <param name="cancellationToken">Current cancellation token</param>
        /// <returns><see cref="OriginResponse"/></returns>
        public async Task<OriginResponse?> LoadAssetFromLocation(AssetId assetId, string location,
            CancellationToken cancellationToken)
        {
            var customerOriginStrategy =
                await customerOriginStrategyRepository.GetCustomerOriginStrategy(assetId, location);

            var originStrategy = originStrategyResolver(customerOriginStrategy.Strategy);

            return await originStrategy.LoadAssetFromOrigin(assetId, location, customerOriginStrategy, cancellationToken);
        }
    }
}