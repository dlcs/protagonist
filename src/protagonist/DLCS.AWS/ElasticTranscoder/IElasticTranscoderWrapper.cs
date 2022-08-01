using Amazon.ElasticTranscoder.Model;
using DLCS.Core.Types;

namespace DLCS.AWS.ElasticTranscoder;

/// <summary>
/// Basic interface for working with AWS ElasticTranscoder
/// </summary>
public interface IElasticTranscoderWrapper
{
    /// <summary>
    /// Get a lookup of preset {name}:{id} (e.g. "System Preset: Generic 1080p": "1351620000001-000001")
    /// </summary>
    /// <param name="token">CancellationToken</param>
    /// <returns>Dictionary of ElasticTranscoder presets</returns>
    Task<Dictionary<string, string>> GetPresetIdLookup(CancellationToken token = default);

    /// <summary>
    /// Get ElasticTranscoder Pipeline Id from name. 
    /// </summary>
    /// <param name="pipelineName">Preconfigured pipeline Id</param>
    /// <param name="token">CancellationToken</param>
    /// <returns>ElasticTranscoder Pipeline Id, if found</returns>
    Task<string?> GetPipelineId(string pipelineName, CancellationToken token = default);

    /// <summary>
    /// Create an ElasticTranscoder job using specified details. Uses "auto" for framerate, aspectRatio etc.
    /// Adds "dlcsId", "startTime" and "jobId" to user metadata
    /// </summary>
    /// <param name="assetId">Asset job is for. Added as "dlcsId" metadata</param>
    /// <param name="inputKey">The s3:// URI for item in input bucket</param>
    /// <param name="pipelineId">Id of pipeline to use for transcoding media</param>
    /// <param name="outputs">A list of outputs for </param>
    /// <param name="jobId">Unique identifier for job. Added as "jobId" metadata.</param>
    /// <param name="token">CancellationToken</param>
    /// <returns><see cref="CreateJobResponse"/> object</returns>
    Task<CreateJobResponse> CreateJob(AssetId assetId, string inputKey, string pipelineId,
        List<CreateJobOutput> outputs, string jobId, CancellationToken token = default);
}