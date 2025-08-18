using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Types;
using DLCS.Model.Assets.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DLCS.Repository.Assets;

public class AssetApplicationMetadataRepository : IAssetApplicationMetadataRepository
{
    private readonly DlcsContext dlcsContext;
    private readonly ILogger<AssetApplicationMetadataRepository> logger;

    public AssetApplicationMetadataRepository(DlcsContext dlcsContext, ILogger<AssetApplicationMetadataRepository> logger)
    {
        this.dlcsContext = dlcsContext;
        this.logger = logger;
    }
    
    public async Task<bool> DeleteAssetApplicationMetadata(AssetId assetId, string metadataType,
        CancellationToken cancellationToken = default)
    {
        var assetApplicationMetadata = await dlcsContext.AssetApplicationMetadata
            .SingleOrDefaultAsync(i => i.AssetId == assetId && i.MetadataType == metadataType, cancellationToken);
        
        if (assetApplicationMetadata == null)
        {
            logger.LogDebug("Attempt to delete non-existent asset metadata {MetadataType} for {AssetId}", metadataType,
                assetId);
            return false;
        }

        dlcsContext.AssetApplicationMetadata.Remove(assetApplicationMetadata);
        await dlcsContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
