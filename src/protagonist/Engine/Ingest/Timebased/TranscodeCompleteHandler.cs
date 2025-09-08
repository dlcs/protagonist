using System.Text.Json;
using System.Text.Json.Serialization;
using DLCS.AWS.MediaConvert.Models;
using DLCS.AWS.SQS;
using Engine.Ingest.Timebased.Completion;

namespace Engine.Ingest.Timebased;

/// <summary>
/// Handler for Transcode Completion messages.
/// </summary>
public class TranscodeCompleteHandler(
    ITimebasedIngestorCompletion timebasedIngestorCompletion,
    ILogger<TranscodeCompleteHandler> logger)
    : IMessageHandler
{
    private static readonly JsonSerializerOptions Settings = new(JsonSerializerDefaults.Web)
    {
       NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public async Task<bool> HandleMessage(QueueMessage message, CancellationToken cancellationToken)
    {
        var mediaConvertNotification = DeserializeBody(message);
        
        if (mediaConvertNotification == null) return false;
        
        var jobId = mediaConvertNotification.Detail.JobId;
        var assetId = mediaConvertNotification.GetAssetId();

        if (assetId == null)
        {
            logger.LogWarning("Unable to find DlcsId in message for MC job {JobId}", jobId);
            return false;
        }
        
        var batchId = mediaConvertNotification.GetBatchId();

        logger.LogTrace("Received Message {MessageId} for {AssetId}, batch {BatchId}", message.MessageId, assetId,
            batchId ?? 0);

        var success =
            await timebasedIngestorCompletion.CompleteSuccessfulIngest(assetId, batchId, jobId, cancellationToken);

        logger.LogInformation("Message {MessageId} handled for {AssetId} with result {IngestResult}", message.MessageId,
            assetId, success);

        return true;
    }
    
    private TranscodedNotification? DeserializeBody(QueueMessage message)
    {
        try
        {
            var mediaConvertNotification = message.Body.Deserialize<TranscodedNotification>(Settings);
            return mediaConvertNotification;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deserializing transcode notification {Message}", message.Body);
            return null;
        }
    }
}
