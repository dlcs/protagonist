using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Repository;
using DLCS.Repository.Assets;

namespace CleanupHandler.Repository;

public class CleanupHandlerAssetRepository : ICleanupHandlerAssetRepository
{
    private readonly DlcsContext dbContext;
    
    public CleanupHandlerAssetRepository(DlcsContext dbContext)
    {
        this.dbContext = dbContext;
    }
    
    public Asset? RetrieveAssetWithDeliveryChannels(AssetId assetId)
    {
        return dbContext.Images.IncludeDeliveryChannelsWithPolicy().SingleOrDefault(x => x.Id == assetId);
    }
}