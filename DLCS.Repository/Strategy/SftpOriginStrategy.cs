using System;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Assets;
using DLCS.Model.Customer;

namespace DLCS.Repository.Strategy
{
    /// <summary>
    /// OriginStrategy implementation for 'sftp' assets.
    /// </summary>
    public class SftpOriginStrategy : SafetyCheckOriginStrategy
    {
        public override OriginStrategyType Strategy => OriginStrategyType.SFTP;

        protected override Task<OriginResponse?> LoadAssetFromOriginImpl(Asset asset,
            CustomerOriginStrategy customerOriginStrategy, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}