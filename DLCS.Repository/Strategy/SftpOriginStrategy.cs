using System;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Guard;
using DLCS.Core.Types;
using DLCS.Model.Customer;

namespace DLCS.Repository.Strategy
{
    /// <summary>
    /// OriginStrategy implementation for 'sftp' assets.
    /// </summary>
    public class SftpOriginStrategy : IOriginStrategy
    {
        public Task<OriginResponse?> LoadAssetFromOrigin(AssetId assetId, string origin,
            CustomerOriginStrategy? customerOriginStrategy, CancellationToken cancellationToken = default)
        {
            customerOriginStrategy.ThrowIfNull(nameof(customerOriginStrategy));
            throw new NotImplementedException();
        }
    }
}