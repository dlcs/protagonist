using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Types;
using DLCS.Model.Assets;

namespace DLCS.Repository.Assets
{
    /// <summary>
    /// Implementation of <see cref="IAssetRepository"/> using EFCore for data access.
    /// </summary>
    public class AssetRepository : IAssetRepository
    {
        private readonly DlcsContext dlcsContext;

        public AssetRepository(DlcsContext dlcsContext)
        {
            this.dlcsContext = dlcsContext;
        }

        public async Task<Asset?> GetAsset(string id)
            => await dlcsContext.Images.FindAsync(id);

        public async Task<Asset?> GetAsset(AssetId id)
            => await GetAsset(id.ToString());

        public async Task<ImageLocation> GetImageLocation(AssetId assetId)
            => await dlcsContext.ImageLocations.FindAsync(assetId.ToString());

        public Task<PageOfAssets> GetPageOfAssets(int customerId, int spaceId, int page, int pageSize, string orderBy, bool ascending,
            CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }
    }
}