using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Types;

namespace DLCS.Model.Assets
{
    public interface IAssetRepository
    {
        public Task<Asset?> GetAsset(string id);
        
        public Task<Asset?> GetAsset(AssetId id);
        
        public Task<Asset?> GetAsset(string id, bool noCache);
        
        public Task<Asset?> GetAsset(AssetId id, bool noCache);

        public Task<ImageLocation> GetImageLocation(AssetId assetId);
        
        public Task<PageOfAssets?> GetPageOfAssets(
            int customerId, int spaceId, int page, int pageSize, 
            string orderBy, bool descending,
            CancellationToken cancellationToken);

        public Task Save(Asset asset, CancellationToken cancellationToken);
    }
}
