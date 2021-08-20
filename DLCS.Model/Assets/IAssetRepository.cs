using System.Threading.Tasks;
using DLCS.Core.Types;

namespace DLCS.Model.Assets
{
    public interface IAssetRepository
    {
        public ValueTask<Asset?> GetAsset(string id);
        
        public ValueTask<Asset?> GetAsset(AssetId id);

        public ValueTask<ImageLocation> GetImageLocation(AssetId assetId);
    }
}
