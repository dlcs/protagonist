using System.Text.Json.Serialization;
using Amazon.ElasticTranscoder.Model;
using DLCS.Core.Exceptions;
using DLCS.Core.Types;

namespace DLCS.AWS.ElasticTranscoder.Models;

/// <summary>
/// The body of a notification sent out from ElasticTranscoder.
/// </summary>
/// <remarks>See https://docs.aws.amazon.com/elastictranscoder/latest/developerguide/notifications.html</remarks>
public class TranscodedNotification
{
    /// <summary>
    /// The State of the job (PROGRESSING|COMPLETED|WARNING|ERROR)
    /// </summary>
    public string State { get; set; }
    
    /// <summary>
    /// Api version used to create job
    /// </summary>
    public string Version { get; set; }
    
    /// <summary>
    /// Value of Job:Id object that ET returns in response to a Create Job Request
    /// </summary>
    public string JobId { get; set; }
    
    /// <summary>
    /// Value of PipelineId in Create Job Request
    /// </summary>
    public string PipelineId { get; set; }
    
    /// <summary>
    /// Job input settings
    /// </summary>
    /// <remarks>JobInput is from AWS ElasticTranscoder nuget</remarks>
    public JobInput Input { get; set; }
    
    /// <summary>
    /// The code of any error that occurred
    /// </summary>
    public int? ErrorCode { get; set; }
    
    /// <summary>
    /// Prefix for filenames in Amazon S3 bucket
    /// </summary>
    public string? OutputKeyPrefix { get; set; }
    
    public int InputCount { get; set; }
    
    public List<TranscodeOutput> Outputs { get; set; }
    
    public Dictionary<string, string> UserMetadata { get; set; }

    /// <summary>
    /// Get the AssetId for this job from user metadata
    /// </summary>
    public AssetId? GetAssetId()
    {
        try
        {
            return UserMetadata.TryGetValue(UserMetadataKeys.DlcsId, out var rawAssetId)
                ? AssetId.FromString(rawAssetId)
                : null;
        }
        catch (InvalidAssetIdException)
        {
            return null;
        }
    }

    /// <summary>
    /// Get the BatchId, if found, for this job from user metadata
    /// </summary>
    public int? GetBatchId()
    {
        if (!UserMetadata.TryGetValue(UserMetadataKeys.BatchId, out var rawBatchId)) return null;

        return int.TryParse(rawBatchId, out var batchId) ? batchId : null;
    }
}