using System.Threading.Tasks;
using DLCS.Core;
using DLCS.Core.Caching;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using LazyCache;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DLCS.Repository.Assets;

/// <summary>
/// Implementation of <see cref="IAssetRepository"/> using EFCore for data access.
/// </summary>
public class AssetRepository : AssetRepositoryCachingBase
{
    private readonly DlcsContext dlcsContext;

    public AssetRepository(DlcsContext dlcsContext,
        IAppCache appCache,
        IOptions<CacheSettings> cacheOptions,
        ILogger<AssetRepository> logger) : base(appCache, cacheOptions, logger)
    {
        this.dlcsContext = dlcsContext;
    }

    public override async Task<ImageLocation?> GetImageLocation(AssetId assetId)
        => await dlcsContext.ImageLocations.FindAsync(assetId.ToString());

    protected override Task<ResultStatus<DeleteResult>> DeleteAssetFromDatabase(string id)
    {
        throw new System.NotImplementedException();
    }

    protected override async Task<Asset?> GetAssetFromDatabase(string id)
        => await dlcsContext.Images.FindAsync(id);
}