using System.Threading.Tasks;

namespace DLCS.Model.Assets
{
    public interface IAssetRepository
    {
        public Task<Asset?> GetAsset(string id);
    }
}
