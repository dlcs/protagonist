using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Customer;

namespace DLCS.Repository.Strategy
{
    /// <summary>
    /// Base interface for implementations of different Origin Strategies.
    /// </summary>
    public interface IOriginStrategy
    {
        /// <summary>
        /// Loads specified <see cref="Asset"/> from origin, using details in specified <see cref="CustomerOriginStrategy"/>
        /// </summary>
        /// <returns>Asset as response</returns>
        public Task<OriginResponse?> LoadAssetFromOrigin(AssetId assetId, string origin,
            CustomerOriginStrategy? customerOriginStrategy, CancellationToken cancellationToken = default);
    }
    
    public class HttpClients
    {
        /// <summary>
        /// Named http client for fetching origin via Http request.
        /// </summary>
        public const string OriginStrategy = "OriginStrategy";
    }
}