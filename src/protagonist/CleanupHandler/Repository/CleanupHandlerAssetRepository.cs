using DLCS.Core.Types;
using DLCS.Repository;
using Microsoft.EntityFrameworkCore;

namespace CleanupHandler.Repository;

public class CleanupHandlerAssetRepository : ICleanupHandlerAssetRepository
{
    private readonly DlcsContext dbContext;

    public CleanupHandlerAssetRepository(DlcsContext dbContext)
    {
        this.dbContext = dbContext;
    }
    
    public Task<bool> CheckExists(AssetId assetId) => dbContext.Images.AnyAsync(x => x.Id == assetId);
}