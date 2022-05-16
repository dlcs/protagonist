using System.Threading.Tasks;

namespace DLCS.Model.Assets
{
    public interface IImageOptimisationPolicyRepository
    {
        Task<ImageOptimisationPolicy?> GetImageOptimisationPolicy(string id);
    }
}