using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Types;
using DLCS.Model.Assets.Metadata;

namespace DLCS.Repository.Assets;

public class AssetApplicationMetadataRepository : IAssetApplicationMetadataRepository
{
    private readonly DlcsContext dlcsContext;

    public AssetApplicationMetadataRepository(DlcsContext dlcsContext)
    {
        this.dlcsContext = dlcsContext;
    }
    
    public async Task<List<int[]>> GetThumbnailSizes(AssetId assetId)
    {


        return new List<int[]>();
    }

    public async Task<AssetApplicationMetadata> AddApplicationMetadata(
        AssetApplicationMetadata metadata, 
        CancellationToken cancellationToken = default)
    {
        var databaseMetadata= await dlcsContext.AssetApplicationMetadata.AddAsync(metadata, cancellationToken);
        await dlcsContext.SaveChangesAsync(cancellationToken);

        return databaseMetadata.Entity;
    }
}