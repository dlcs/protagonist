using DLCS.Core.Types;
using DLCS.Model.Assets;
using Engine.Ingest.Timebased;

namespace Engine.Ingest.Completion;

public interface ITimebasedIngestorCompletion
{
    /// <summary>
    /// Mark asset as completed in database. Move assets from Transcode output to main storage location.
    /// </summary>
    /// <param name="assetId">Id of asset running completion operations for</param>
    /// <param name="transcodeResult">Result of transcode operation</param>
    /// <returns>Value representing success</returns>
    Task<bool> CompleteSuccessfulIngest(AssetId assetId, TranscodeResult transcodeResult,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark Asset as completed in database, creating ImageLocation + ImageStorage record where appropriate.
    /// </summary>
    /// <param name="asset">Asset to finalise</param>
    /// <param name="assetSize">Size of asset, if completed successfully</param>
    /// <returns>Value representing success</returns>
    Task<bool> CompleteAssetInDatabase(Asset asset, long? assetSize = null,
        CancellationToken cancellationToken = default);
}