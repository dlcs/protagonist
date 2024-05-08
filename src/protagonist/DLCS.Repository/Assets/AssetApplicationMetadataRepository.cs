using System;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Types;
using DLCS.Model.Assets.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Z.EntityFramework.Plus;

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

    /// <inheritdoc />
    /// <remarks>
    /// Once we have more usage of AssetApplicationMetadata we should endeavour to link methods that write data to the
    /// extension methods that read the data to avoid one changing without the other.
    /// </remarks>
    public async Task<AssetApplicationMetadata> UpsertApplicationMetadata(AssetId assetId, string metadataType, string metadataValue,
        CancellationToken cancellationToken = default)
    {
        var addedMetadata =  await dlcsContext.AssetApplicationMetadata.FirstOrDefaultAsync(e =>
            e.AssetId == assetId && e.MetadataType == metadataType, cancellationToken);

        if (addedMetadata is not null)
        {
            addedMetadata.MetadataValue = metadataValue;
            addedMetadata.Modified = DateTime.UtcNow;
            await dlcsContext.SaveChangesAsync(cancellationToken);
            return addedMetadata;
        }

        var assetApplicationMetadata = new AssetApplicationMetadata()
        {
            AssetId = assetId,
            MetadataType = metadataType,
            MetadataValue = metadataValue,
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow
        };
        
        await dlcsContext.AssetApplicationMetadata.AddAsync(assetApplicationMetadata, cancellationToken);
        
        await dlcsContext.SaveChangesAsync(cancellationToken);
        return assetApplicationMetadata;
    }

    public async Task<bool> DeleteAssetApplicationMetadata(AssetId assetId, string metadataType,
        CancellationToken cancellationToken = default)
    {
        var asset = await dlcsContext.AssetApplicationMetadata
            .SingleOrDefaultAsync(i => i.AssetId == assetId && i.MetadataType == metadataType, cancellationToken);
        
        if (asset == null)
        {
            logger.LogDebug("Attempt to delete non-existent asset metadata {MetadataType} for {AssetId}", metadataType,
                assetId);
            return false;
        }

        await dlcsContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}