using DLCS.AWS.MediaConvert.Models;
using DLCS.AWS.S3;
using DLCS.AWS.Settings;
using DLCS.AWS.Transcoding;
using DLCS.Model.Assets;
using DLCS.Model.Policies;
using Microsoft.Extensions.Options;

namespace Engine.Ingest.Timebased.Transcode;

/// <summary>
/// Implementation of <see cref="IMediaTranscoder"/> using AWS Elemental MediaConvert for transcoding
/// </summary>
public class MediaConvert(
    ITranscoderWrapper transcoderWrapper,
    ITranscoderPresetLookup transcoderPresetLookup,
    IStorageKeyGenerator storageKeyGenerator,
    IOptionsMonitor<AWSSettings> awsSettings,
    ILogger<MediaConvert> logger)
    : IMediaTranscoder
{
    public async Task<bool> InitiateTranscodeOperation(IngestionContext context, Dictionary<string, string> jobMetadata,
        CancellationToken token = default)
    {
        // Get the queue arn from the name
        var queueName = GetQueueName();
        var queueArn = await transcoderWrapper.GetPipelineId(queueName, token);

        if (string.IsNullOrEmpty(queueArn))
        {
            logger.LogWarning("Queue Arn not found for {QueueName}", queueName);
            context.Asset.Error = "Could not find MediaConvert queue";
            return false;
        }
        
        var output = GetJobOutput(context, jobMetadata);
        if (output.Outputs.Count == 0)
        {
            context.Asset.Error = "Unable to generate MediaConvert outputs";
            return false;
        }
        
        jobMetadata[TranscodeMetadataKeys.StartTime] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

        var mediaConvertJob =
            await transcoderWrapper.CreateJob(context.AssetFromOrigin!.Location, queueArn, output, jobMetadata, token);

        logger.LogDebug("Created MediaConvert job {MCJobId}, got response {StatusCode}", mediaConvertJob.JobId,
            mediaConvertJob.HttpStatusCode);

        if (!mediaConvertJob.Success)
        {
            context.Asset.Error =
                $"Create MediaConvert job failed with status {(int)mediaConvertJob.HttpStatusCode}|{mediaConvertJob.HttpStatusCode}";
            return false;
        }

        await transcoderWrapper.PersistJobId(context.AssetId, mediaConvertJob.JobId, token);
        return true;
    }

    private MediaConvertJobGroup GetJobOutput(IngestionContext context, Dictionary<string, string> jobMetadata)
    {
        // Guid to uniquely identify this job - this is added to ET output path to avoid overwriting by separate jobs 
        var jobId = Guid.NewGuid().ToString();
        jobMetadata[TranscodeMetadataKeys.JobId] = jobId;
        
        var assetId = context.AssetId;
        
        // Policy-data gives us the 'friendly' name to use (e.g. "audio-mp3-128" or "exhibition-quality")
        var timeBasedPolicies = context.Asset.ImageDeliveryChannels
            .GetTimebasedChannel(true)!.DeliveryChannelPolicy.AsTimebasedPresets();

        // Get the preset lookup keyed by policy-data name
        var presetLookup = transcoderPresetLookup.GetPresetLookupByPolicyName();
        
        var outputs = new List<MediaConvertOutput>(timeBasedPolicies.Count);
        foreach (var timeBasedPolicy in timeBasedPolicies)
        {
            if (!presetLookup.TryGetValue(timeBasedPolicy, out var transcoderPreset))
            {
                logger.LogWarning("Unable to find preset {TimeBasedPolicy}. Check configuration.", timeBasedPolicy);
                continue;
            }
            
            outputs.Add(new MediaConvertOutput(transcoderPreset.Id, transcoderPreset.Extension));

            logger.LogTrace("Asset {AssetId} will have output for '{TechnicalDetail}'", assetId, timeBasedPolicy);
        }
        
        var destination = storageKeyGenerator.GetTranscodeDestinationRoot(assetId, jobId);
        logger.LogDebug("Asset {AssetId} will be transcoded to '{TranscodeDestination}'", assetId, destination);
        return new MediaConvertJobGroup(destination, outputs); 
    }

    private string GetQueueName() => awsSettings.CurrentValue.Transcode.QueueName;
}
