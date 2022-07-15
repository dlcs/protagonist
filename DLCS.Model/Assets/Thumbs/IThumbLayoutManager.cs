using System.Collections.Generic;
using System.Threading.Tasks;
using DLCS.Core.Types;

namespace DLCS.Model.Assets.Thumbs
{
    public interface IThumbLayoutManager
    {
        /// <summary>
        /// Ensure S3 bucket has new thumbnail layout.
        /// </summary>
        /// <param name="assetId"><see cref="AssetId"/> to create new layout for</param>
        /// <returns><see cref="ReorganiseResult"/> enum representing result</returns>
        Task<ReorganiseResult> EnsureNewLayout(AssetId assetId);

        /// <summary>
        /// Create new thumbs in S3 from provided images on disk
        /// </summary>
        /// <param name="asset">Asset thumbnails are for</param>
        /// <param name="thumbsToProcess">List of jpgs on disk that are to be copied to S3</param>
        /// <returns></returns>
        Task CreateNewThumbs(Asset asset, IReadOnlyList<ImageOnDisk> thumbsToProcess);
    }
}