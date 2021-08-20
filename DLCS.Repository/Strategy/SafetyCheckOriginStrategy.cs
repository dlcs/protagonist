using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Guard;
using DLCS.Core.Types;
using DLCS.Model.Customer;

namespace DLCS.Repository.Strategy
{
    /// <summary>
    /// Implementation of <see cref="IOriginStrategy"/> that provides argument checking.
    /// </summary>
    public abstract class SafetyCheckOriginStrategy : IOriginStrategy
    {
        protected abstract Task<OriginResponse?> LoadAssetFromOriginImpl(AssetId assetId, string origin,
            CustomerOriginStrategy customerOriginStrategy, CancellationToken cancellationToken = default);
        
        public Task<OriginResponse?> LoadAssetFromOrigin(AssetId assetId, string origin, CustomerOriginStrategy customerOriginStrategy,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            customerOriginStrategy.ThrowIfNull(nameof(customerOriginStrategy));
            assetId.ThrowIfNull(nameof(assetId));
            origin.ThrowIfNullOrWhiteSpace(nameof(origin));
            
            return LoadAssetFromOriginImpl(assetId, origin, customerOriginStrategy, cancellationToken);
        }
    }
}