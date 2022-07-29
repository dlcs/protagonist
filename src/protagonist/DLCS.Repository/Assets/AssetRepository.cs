﻿using System.Threading.Tasks;
using DLCS.Core.Types;
using DLCS.Model.Assets;

namespace DLCS.Repository.Assets;

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

    public Task<Asset?> GetAsset(string id) => GetAsset(id, false);

    public Task<Asset?> GetAsset(AssetId id) => GetAsset(id, false);

    public async Task<Asset?> GetAsset(string id, bool noCache)
        => await dlcsContext.Images.FindAsync(id);

    public async Task<Asset?> GetAsset(AssetId id, bool noCache)
        => await GetAsset(id.ToString());

    public async Task<ImageLocation> GetImageLocation(AssetId assetId)
        => await dlcsContext.ImageLocations.FindAsync(assetId.ToString());
}