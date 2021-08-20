using System.Collections.Generic;
using System.Threading.Tasks;
using IIIF.ImageApi;

namespace DLCS.Model.Assets
{
    public interface IThumbRepository
    {
        /// <summary>
        /// Get <see cref="ThumbnailResponse"/> object, containing actual thumbnail bytes
        /// </summary>
        Task<ThumbnailResponse> GetThumbnail(int customerId, int spaceId, ImageRequest imageRequest);
        
        /// <summary>
        /// Get a list of all open thumbnails for specified image.
        /// </summary>
        Task<List<int[]>?> GetOpenSizes(int customerId, int spaceId, ImageRequest imageRequest);

        /// <summary>
        /// Get thumbnail <see cref="SizeCandidate"/> for specified request.
        /// </summary>
        Task<SizeCandidate?> GetThumbnailSizeCandidate(int customerId, int spaceId, ImageRequest imageRequest);
    }
}
