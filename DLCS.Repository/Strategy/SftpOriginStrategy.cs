using System;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Types;
using DLCS.Model.Customer;

namespace DLCS.Repository.Strategy
{
    /// <summary>
    /// OriginStrategy implementation for 'sftp' assets.
    /// </summary>
    public class SftpOriginStrategy : SafetyCheckOriginStrategy
    {
        protected override Task<OriginResponse?> LoadAssetFromOriginImpl(AssetId assetId, string origin,
            CustomerOriginStrategy customerOriginStrategy, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}