using System.Threading.Tasks;
using DLCS.Core.Types;
using DLCS.Model.Assets.Thumbs;

namespace Thumbs.Reorganising;

public interface IThumbReorganiser
{
    /// <summary>
    /// Ensure S3 bucket has new thumbnail layout.
    /// </summary>
    /// <param name="assetId"><see cref="AssetId"/> to create new layout for</param>
    /// <returns><see cref="ReorganiseResult"/> enum representing result</returns>
    Task<ReorganiseResult> EnsureNewLayout(AssetId assetId);
}