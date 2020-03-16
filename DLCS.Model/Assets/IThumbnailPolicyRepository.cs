using System.Threading.Tasks;

namespace DLCS.Model.Assets
{
    public interface IThumbnailPolicyRepository
    {
        Task<ThumbnailPolicy> GetThumbnailPolicy(string thumbnailPolicyId);
    }
}