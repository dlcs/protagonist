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

        public ValueTask<Asset?> GetAsset(string id)
            => dlcsContext.Images.FindAsync(id);

        public ValueTask<Asset?> GetAsset(AssetId id)
            => GetAsset(id.ToString());

        public ValueTask<ImageLocation> GetImageLocation(AssetId assetId)
            => dlcsContext.ImageLocations.FindAsync(assetId.ToString());
    }
}