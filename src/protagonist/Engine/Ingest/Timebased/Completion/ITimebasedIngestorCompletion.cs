using DLCS.Core.Types;

namespace Engine.Ingest.Timebased.Completion;

public interface ITimebasedIngestorCompletion
{
    /// <summary>
    /// Mark asset as completed in database. Move assets from Transcode output to main storage location.
    /// </summary>
    /// <param name="assetId">Id of asset running completion operations for</param>
    /// <param name="batchId">The id of batch this ingest operation is for</param>
    /// <param name="jobId">JobId of completed transcode job</param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <returns>Value representing success</returns>
    Task<bool> CompleteSuccessfulIngest(AssetId assetId, int? batchId, string jobId,
        CancellationToken cancellationToken = default);
}
