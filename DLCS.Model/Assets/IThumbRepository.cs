using System.Collections.Generic;
using System.Threading.Tasks;
using DLCS.Model.Storage;
using IIIF.ImageApi;

namespace DLCS.Model.Assets
{
    public interface IThumbRepository
    {
        // question... these could just take ThumbnailRequest from DLCS.Web.
        // But I that represents the full path of a web request.
        // it's fine for this to know about the IIIF part of the request path, as that's an
        // external context.

        public Task<ObjectInBucket> GetThumbLocation(int customerId, int spaceId, ImageRequest imageRequest);
        public Task<List<int[]>> GetSizes(int customerId, int spaceId, ImageRequest imageRequest);
        public ThumbnailPolicy GeThumbnailPolicy(string thumbnailPolicyId);
    }
}
