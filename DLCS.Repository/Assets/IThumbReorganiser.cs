using System.Threading.Tasks;
using DLCS.Core.Types;

namespace DLCS.Repository.Assets
{
    public interface IThumbReorganiser
    {
        /// <summary>
        /// Ensure S3 bucket has new thumbnail layout.
        /// </summary>
        /// <param name="assetId"><see cref="AssetId"/> to create new layout for</param>
        /// <returns><see cref="ReorganiseResult"/> enum representing result</returns>
        Task<ReorganiseResult> EnsureNewLayout(AssetId assetId);
    }

    /// <summary>
    /// Default, no-op implementation of <see cref="IThumbReorganiser"/>
    /// </summary>
    public class NonOrganisingReorganiser : IThumbReorganiser
    {
        public Task<ReorganiseResult> EnsureNewLayout(AssetId assetId)
            => Task.FromResult(ReorganiseResult.Unknown);
    }
}