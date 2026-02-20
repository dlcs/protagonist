using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Assets;
using DLCS.Model.Customers;
using DLCS.Repository.Strategy.DependencyInjection;

namespace DLCS.Repository.Strategy.Utils;

/// <summary>
/// Helper class that gets appropriate origin strategy for a resource and fetches from origin to local disk
/// </summary>
public class OriginFetcher(
    ICustomerOriginStrategyRepository customerOriginStrategyRepository,
    OriginStrategyResolver originStrategyResolver)
{
    /// <summary>
    /// Get <see cref="OriginResponse"/> object for provided asset, loading from origin passed origin strategy
    /// </summary>
    /// <param name="originItem">Item that has an origin, used to fetch it</param>
    /// <param name="customerOriginStrategy">OriginStrategy to use</param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <returns><see cref="OriginResponse"/></returns>
    public async Task<OriginResponse> LoadFromOrigin(IOriginItem originItem,
        CustomerOriginStrategy customerOriginStrategy, CancellationToken cancellationToken)
    {
        var originStrategy = originStrategyResolver(customerOriginStrategy.Strategy);

        return await originStrategy.LoadFromOrigin(originItem, customerOriginStrategy,
            cancellationToken);
    }
}
