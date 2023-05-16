using DLCS.AWS.ElasticTranscoder.Models;
using DLCS.Core.Types;

namespace Engine.Ingest.Timebased.Completion;

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
}