using System.Text.Json;
using System.Text.Json.Serialization;
using DLCS.AWS.ElasticTranscoder.Models;
using DLCS.AWS.SQS;
using DLCS.AWS.SQS.Models;
using Engine.Ingest.Timebased.Completion;

namespace Engine.Ingest.Timebased;

/// <summary>
/// Handler for Transcode Completion messages.
/// </summary>
public class TranscodeCompleteHandler : IMessageHandler
{
    private readonly ITimebasedIngestorCompletion timebasedIngestorCompletion;
    private readonly ILogger<TranscodeCompleteHandler> logger;
    private static readonly JsonSerializerOptions Settings = new(JsonSerializerDefaults.Web)
    {
       NumberHandling = JsonNumberHandling.WriteAsString
    };

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

        var assetId = elasticTranscoderMessage.GetAssetId();
        
        if (assetId == null)
        {
            logger.LogWarning("Unable to find DlcsId in message for ET job {JobId}", elasticTranscoderMessage.JobId);
            return false;
        }

        logger.LogTrace("Received Message {MessageId} for {AssetId}", message.MessageId, assetId);

        var transcodeResult = new TranscodeResult(elasticTranscoderMessage);

        var success =
            await timebasedIngestorCompletion.CompleteSuccessfulIngest(assetId, transcodeResult, cancellationToken);

        logger.LogInformation("Message {MessageId} handled for {AssetId} with result {IngestResult}", message.MessageId,
            assetId, success);
        
        // TODO - return false so that the message is deleted from the queue in all instances.
        // This shouldn't be the case and can be revisited at a later date as it will need logic of how Batch.Errors is
        // calculated
        return true;
    }
    
    private TranscodedNotification? DeserializeBody(QueueMessage message)
    {
        try
        {
            var notification = message.Body.Deserialize<SNSToSQSEnvelope>(Settings);
            var elasticTranscoderMessage =
                JsonSerializer.Deserialize<TranscodedNotification>(notification.Message, Settings);
            return elasticTranscoderMessage;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deserializing transcode complete message {Message}", message.Body);
            return null;
        }
    }
}