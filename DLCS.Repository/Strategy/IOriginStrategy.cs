using System.Threading;
using System.Threading.Tasks;
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
        /// The <see cref="OriginStrategy"/> that this implementation handles.
        /// </summary>
        public OriginStrategy Strategy { get; }

        /// <summary>
        /// Loads specified <see cref="Asset"/> from origin, using details in specified <see cref="CustomerOriginStrategy"/>
        /// </summary>
        /// <param name="asset"></param>
        /// <param name="customerOriginStrategy"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>Asset as Stream</returns>
        public Task<OriginResponse?> LoadAssetFromOrigin(Asset asset, CustomerOriginStrategy customerOriginStrategy,
            CancellationToken cancellationToken = default);
    }
    
    public class HttpClients
    {
        /// <summary>
        /// Named http client for fetching origin via Http request.
        /// </summary>
        public const string OriginStrategy = "OriginStrategy";
    }
}