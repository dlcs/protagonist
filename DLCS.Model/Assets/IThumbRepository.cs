using System.Collections.Generic;
using System.Threading.Tasks;
using DLCS.Core.Types;
using IIIF.ImageApi;

namespace DLCS.Model.Assets
{
    public interface IThumbRepository
    {
        /// <summary>
        /// Get <see cref="ThumbnailResponse"/> object, containing actual thumbnail bytes
        /// </summary>
        Task<ThumbnailResponse> GetThumbnail(AssetId assetId, ImageRequest imageRequest);
        
        /// <summary>
        /// Get a list of all open thumbnails for specified image.
        /// </summary>
        Task<List<int[]>?> GetOpenSizes(AssetId assetId);

        /// <summary>
        /// Get thumbnail <see cref="SizeCandidate"/> for specified request.
        /// </summary>
        Task<SizeCandidate?> GetThumbnailSizeCandidate(AssetId assetId, ImageRequest imageRequest);
    }
}
