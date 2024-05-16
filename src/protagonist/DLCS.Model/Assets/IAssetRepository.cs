using System.Threading.Tasks;
using DLCS.Core.Types;

namespace DLCS.Model.Assets;

public interface IAssetRepository
{
    public Task<Asset?> GetAsset(AssetId assetId);
    
    public Task<Asset?> GetAsset(AssetId assetId, bool noCache);

    public Task<ImageLocation?> GetImageLocation(AssetId assetId);
}