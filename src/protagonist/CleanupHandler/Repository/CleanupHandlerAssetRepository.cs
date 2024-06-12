using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Repository;
using DLCS.Repository.Assets;
using Microsoft.EntityFrameworkCore;

namespace CleanupHandler.Repository;

public class CleanupHandlerAssetRepository : ICleanupHandlerAssetRepository
{
    private readonly DlcsContext dbContext;
    
    public CleanupHandlerAssetRepository(DlcsContext dbContext)
    {
        this.dbContext = dbContext;
    }
    
    public async Task<Asset?> RetrieveAssetWithDeliveryChannels(AssetId assetId)
    {
        return await dbContext.Images.IncludeDeliveryChannelsWithPolicy().SingleOrDefaultAsync(x => x.Id == assetId);
    }
}