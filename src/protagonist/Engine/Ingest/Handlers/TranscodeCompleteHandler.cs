using System.Text.Json;
using Amazon.ElasticTranscoder.Model;
using DLCS.AWS.SQS;
using DLCS.AWS.SQS.Models;
using DLCS.Core.Types;
using Engine.Ingest.Completion;
using Engine.Ingest.Timebased;

namespace Engine.Ingest.Handlers;

/// <summary>
/// Handler for Transcode Completion messages.
/// </summary>
public class TranscodeCompleteHandler : IMessageHandler
{
    private readonly ITimebasedIngestorCompletion timebasedIngestorCompletion;
    private readonly ILogger<TranscodeCompleteHandler> logger;
    private readonly JsonSerializerOptions settings = new(JsonSerializerDefaults.Web);

    public TranscodeCompleteHandler(
        ITimebasedIngestorCompletion timebasedIngestorCompletion,
        ILogger<TranscodeCompleteHandler> logger)
    {
        this.timebasedIngestorCompletion = timebasedIngestorCompletion;
        this.logger = logger;
    }
    
    public async Task<bool> HandleMessage(QueueMessage message, CancellationToken cancellationToken)
    {
        var elasticTranscoderMessage = DeserializeBody(message);

        if (elasticTranscoderMessage == null) return false;
        
        if (!elasticTranscoderMessage.UserMetadata.TryGetValue(UserMetadataKeys.DlcsId, out var rawAssetId))
        {
            logger.LogWarning("Unable to find DlcsId in message for ET job {JobId}", elasticTranscoderMessage.JobId);
            return false;
        }

        var assetId = AssetId.FromString(rawAssetId);
        var transcodeResult = new TranscodeResult(elasticTranscoderMessage);

        var success =
            await timebasedIngestorCompletion.CompleteSuccessfulIngest(assetId, transcodeResult, cancellationToken);
        return success;
    }
    
    private ElasticTranscoderMessage? DeserializeBody(QueueMessage message)
    {
        try
        {
            var notification = message.Body.Deserialize<SNSToSQSEnvelope>(settings);
            var elasticTranscoderMessage =
                JsonSerializer.Deserialize<ElasticTranscoderMessage>(notification.Message, settings);
            return elasticTranscoderMessage;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deserializing transcode complete message {Message}", message.Body);
            return null;
        }
    }
}

/// <summary>
/// The body of a notification sent out from ElasticTranscoder.
/// </summary>
/// <remarks>See https://docs.aws.amazon.com/elastictranscoder/latest/developerguide/notifications.html</remarks>
public class ElasticTranscoderMessage
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
    /// Value of Job:Id object that ET returns in reponse to a Create Job Request
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
    public string? ErrorCode { get; set; }
    
    /// <summary>
    /// Prefix for filenames in Amazon S3 bucket
    /// </summary>
    public string? OutputKeyPrefix { get; set; }
    
    public int InputCount { get; set; }
    
    public List<TranscodeOutput> Outputs { get; set; }
    
    public Dictionary<string, string> UserMetadata { get; set; }
}