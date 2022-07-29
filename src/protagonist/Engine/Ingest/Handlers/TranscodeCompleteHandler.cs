using System.Text.Json;
using Amazon.ElasticTranscoder.Model;
using DLCS.AWS.SQS;
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
        var notification = DeserializeBody(message);

        if (notification == null) return false;
        
        if (!notification.UserMetadata.TryGetValue(UserMetadataKeys.DlcsId, out var rawAssetId))
        {
            logger.LogWarning("Unable to find DlcsId in message for ET job {JobId}", notification.JobId);
            return false;
        }

        var assetId = AssetId.FromString(rawAssetId);
        var transcodeResult = new TranscodeResult(notification.Input.Key, notification.Outputs);

        var success =
            await timebasedIngestorCompletion.CompleteSuccessfulIngest(assetId, transcodeResult, cancellationToken);
        return success;
    }
    
    private ElasticTranscoderMessage? DeserializeBody(QueueMessage message)
    {
        try
        {
            var notification = message.Body.Deserialize<ElasticTranscoderNotification>(settings);
            var elasticTranscoderMessage =
                JsonSerializer.Deserialize<ElasticTranscoderMessage>(notification.Message, settings);
            return elasticTranscoderMessage;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deserializing ET message {Message}", message.Body);
            return null;
        }
    }
}

/// <summary>
/// Represents a notification that has been sent from AWS ElasticTranscoder via SNS.
/// </summary>
internal class ElasticTranscoderNotification
{
    public string Type { get; set; }
    public string MessageId { get; set; }
    public string TopicArn { get; set; }
    public string Subject { get; set; }
    public string Message { get; set; }
    public string Timestamp { get; set; }
    public string SignatureVersion { get; set; }
    public string Signature { get; set; }
    public string SigningCertURL { get; set; }
    public string UnsubscribeURL { get; set; }
}

/// <summary>
/// The body of a notification sent out from ElasticTranscoder.
/// </summary>
/// <remarks>See https://docs.aws.amazon.com/elastictranscoder/latest/developerguide/notifications.html</remarks>
internal class ElasticTranscoderMessage
{
    public string State { get; set; }
    public string Version { get; set; }
    public string JobId { get; set; }
    public string PipelineId { get; set; }
        
    // Note - JobInput is from AWS nuget but is not used
    public JobInput Input { get; set; }
    public string? ErrorCode { get; set; }
    public string? OutputPrefix { get; set; }
    public int InputCount { get; set; }
    public List<TranscodeOutput> Outputs { get; set; }
    public Dictionary<string, string> UserMetadata { get; set; }
}