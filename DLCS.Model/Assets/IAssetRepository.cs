using System.Threading.Tasks;
using DLCS.Core.Types;

namespace DLCS.Model.Assets
{
    public interface IAssetRepository
    {
        public Task<Asset?> GetAsset(string id);
        
        public Task<Asset?> GetAsset(AssetId id);
    }
}
