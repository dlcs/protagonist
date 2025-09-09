using DLCS.AWS.Transcoding.Models.Job;
using DLCS.AWS.Transcoding.Models.Request;
using DLCS.Core.Types;
using CreateJobResponse = DLCS.AWS.Transcoding.Models.Request.CreateJobResponse;

namespace DLCS.AWS.Transcoding;

/// <summary>
/// Basic interface for working with transcoding service
/// </summary>
public interface ITranscoderWrapper
{
    /// <summary>
    /// Get internal pipeline/queue id from it's name. 
    /// </summary>
    /// <param name="pipelineName">Preconfigured pipeline friendly name</param>
    /// <param name="token">CancellationToken</param>
    /// <returns>Internal pipeline Id, if found</returns>
    Task<string?> GetPipelineId(string pipelineName, CancellationToken token = default);

    /// <summary>
    /// Create a transcode job. Transcodes binary at input key into derivatices as specified by <see cref="IJobOutput"/>
    /// output object
    /// </summary>
    /// <param name="inputKey">The location for item to be transcoded</param>
    /// <param name="pipelineId">Id of pipeline to use for transcoding media</param>
    /// <param name="output">Output details for job</param>
    /// <param name="jobMetadata">
    /// Collection of metadata key value pairs to add to job, echoed back on completion
    /// </param>
    /// <param name="token">CancellationToken</param>
    /// <returns><see cref="Models.Request.CreateJobResponse"/> object, contains job Id and overall status code</returns>
    Task<CreateJobResponse> CreateJob(string inputKey, string pipelineId, IJobOutput output,
        Dictionary<string, string> jobMetadata, CancellationToken token = default);
    
    /// <summary>
    /// Persist transcode metadata to storage for later retrieval 
    /// </summary>
    /// <param name="assetId">Asset job is for</param>
    /// <param name="transcoderJobId">Unique identifier for transcoder job</param>
    /// <param name="cancellationToken">Current cancellation token</param>
    Task PersistJobId(AssetId assetId, string transcoderJobId, CancellationToken cancellationToken);

    /// <summary>
    /// Get latest transcode job details for AssetId.
    /// </summary>
    /// <param name="assetId">AssetId to get latest job data for</param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <returns>Job details, if found. Else null</returns>
    Task<TranscoderJob?> GetTranscoderJob(AssetId assetId, CancellationToken cancellationToken);
    
    /// <summary>
    /// Get details of specified transcode job for AssetId
    /// </summary>
    /// <param name="assetId">AssetId job is for</param>
    /// <param name="jobId">Id of job to fetch</param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <returns>Job details, if found and for specified Asset. Else null</returns>
    Task<TranscoderJob?> GetTranscoderJob(AssetId assetId, string jobId, CancellationToken cancellationToken);
}
