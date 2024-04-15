using System;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Types;
using DLCS.Model.Assets.Metadata;
using Microsoft.EntityFrameworkCore;

namespace DLCS.Repository.Assets;

public class AssetApplicationMetadataRepository : IAssetApplicationMetadataRepository
{
    private readonly DlcsContext dlcsContext;

    public AssetApplicationMetadataRepository(DlcsContext dlcsContext)
    {
        this.dlcsContext = dlcsContext;
    }

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
}