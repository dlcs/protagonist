using System.Text.Json;
using DLCS.AWS.ElasticTranscoder.Models;
using DLCS.AWS.SQS;
using DLCS.AWS.SQS.Models;
using Engine.Ingest.Completion;

namespace Engine.Ingest.Handlers;

/// <summary>
/// Handler for Transcode Completion messages.
/// </summary>
public class TranscodeCompleteHandler : IMessageHandler
{
    private readonly ITimebasedIngestorCompletion timebasedIngestorCompletion;
    private readonly ILogger<TranscodeCompleteHandler> logger;
    private static readonly JsonSerializerOptions Settings = new(JsonSerializerDefaults.Web);

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

        var transcodeResult = new TranscodeResult(elasticTranscoderMessage);

        var success =
            await timebasedIngestorCompletion.CompleteSuccessfulIngest(assetId, transcodeResult, cancellationToken);
        return success;
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