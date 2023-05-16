﻿using System.Threading.Tasks;
using DLCS.Core;
using DLCS.Core.Types;

namespace DLCS.Model.Assets;

public interface IAssetRepository
{
    public Task<Asset?> GetAsset(AssetId id);

    public Task<Asset?> GetAsset(AssetId id, bool noCache);

    public Task<ImageLocation?> GetImageLocation(AssetId assetId);

    public Task<ResultStatus<DeleteResult>> DeleteAsset(AssetId assetId);
}
