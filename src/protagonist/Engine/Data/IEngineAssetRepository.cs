using DLCS.Core.Types;
using DLCS.Model.Assets;

namespace Engine.Data;

public interface IEngineAssetRepository
{
    /// <summary>
    /// Update database with ingested asset.
    /// </summary>
    /// <param name="asset">Asset to update</param>
    /// <param name="imageLocation">ImageLocation, optional as may have exited prior to creation</param>
    /// <param name="imageStorage">ImageStorage, optional as may have exited prior to creation</param>
    /// <param name="ingestFinished">
    /// If true then the ingest is done, no further processing required. Else it's an async ingest
    /// and there will be further work required.
    /// </param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <returns>True if successful</returns>
    Task<bool> UpdateIngestedAsset(Asset asset, ImageLocation? imageLocation, ImageStorage? imageStorage,
        bool ingestFinished, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get Asset with specified Id
    /// </summary>
    ValueTask<Asset?> GetAsset(AssetId assetId, CancellationToken cancellationToken = default);
}