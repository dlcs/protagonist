using System.Threading.Tasks;
using DLCS.Core.Types;

namespace Thumbs.Reorganising
{
    /// <summary>
    /// Default, no-op implementation of <see cref="IThumbReorganiser"/>
    /// </summary>
    public class NonOrganisingReorganiser : IThumbReorganiser
    {
        public Task<ReorganiseResult> EnsureNewLayout(AssetId assetId)
            => Task.FromResult(ReorganiseResult.Unknown);
    }
}