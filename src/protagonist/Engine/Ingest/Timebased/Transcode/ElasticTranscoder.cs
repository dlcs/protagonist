using Amazon.ElasticTranscoder.Model;
using DLCS.AWS.ElasticTranscoder;
using DLCS.AWS.Transcoding;
using DLCS.AWS.Transcoding.Models;
using DLCS.Core.Guard;
using DLCS.Model.Assets;
using DLCS.Model.Policies;
using Engine.Ingest.Timebased.Models;
using Engine.Settings;
using Microsoft.Extensions.Options;

namespace Engine.Ingest.Timebased.Transcode;

/// <summary>
/// Implementation of <see cref="IMediaTranscoder"/> using AWS ElasticTranscoder for transcoding
/// </summary>
public class ElasticTranscoder : IMediaTranscoder
{
    private readonly IOptionsMonitor<EngineSettings> engineSettings;
    private readonly ITranscoderWrapper transcoderWrapper;
    private readonly IElasticTranscoderPresetLookup elasticTranscoderPresetLookup;
    private readonly ILogger<ElasticTranscoder> logger;

    public ElasticTranscoder(
        ITranscoderWrapper transcoderWrapper,
        IElasticTranscoderPresetLookup elasticTranscoderPresetLookup,
        IOptionsMonitor<EngineSettings> engineSettings,
        ILogger<ElasticTranscoder> logger)
    {
        this.transcoderWrapper = transcoderWrapper;
        this.elasticTranscoderPresetLookup = elasticTranscoderPresetLookup;
        this.engineSettings = engineSettings;
        engineSettings.CurrentValue.TimebasedIngest.ThrowIfNull(nameof(engineSettings.CurrentValue.TimebasedIngest));
        this.logger = logger;
    }

    public async Task<bool> InitiateTranscodeOperation(IngestionContext context, Dictionary<string ,string> jobMetadata,
        CancellationToken token = default)
    {
        var settings = engineSettings.CurrentValue.TimebasedIngest!;
        var pipelineId = await transcoderWrapper.GetPipelineId(settings.PipelineName, token);

        if (string.IsNullOrEmpty(pipelineId))
        {
            logger.LogWarning("Pipeline Id not found for {PipelineName} to ingest {AssetId}", settings.PipelineName,
                context.AssetId);
            context.Asset.Error = "Could not find ElasticTranscoder pipeline";
            return false;
        }

        var presets = await elasticTranscoderPresetLookup.GetPresetLookupByName(token);

        // Create a guid to uniquely identify this job - this is added to ET output path to avoid overwriting by
        // separate jobs  
        var jobId = Guid.NewGuid().ToString();
        var outputs = GetJobOutputs(context, jobId, settings, presets);

        if (outputs.Count == 0)
        {
            context.Asset.Error = "Unable to generate ElasticTranscoder outputs";
            return false;
        }

        jobMetadata[TranscodeMetadataKeys.JobId] = jobId;
        jobMetadata[TranscodeMetadataKeys.StartTime] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        
        var elasticTranscoderJob = await transcoderWrapper.CreateJob(context.AssetFromOrigin.Location,
            pipelineId, outputs, jobMetadata, token);

        var statusCode = (int)elasticTranscoderJob.HttpStatusCode;

        logger.LogDebug("Created ET job {ETJobId}, got response {StatusCode}", elasticTranscoderJob.Job?.Id,
            elasticTranscoderJob.HttpStatusCode);

        if (statusCode is not (>= 200 and < 300))
        {
            context.Asset.Error = $"Create ElasticTranscoder job failed with status {statusCode}";
            return false;
        }

        await transcoderWrapper.PersistJobId(context.AssetId, elasticTranscoderJob.Job.Id, token);
        return true;
    }

    private List<CreateJobOutput> GetJobOutputs(IngestionContext context, string jobId,
        TimebasedIngestSettings settings, Dictionary<string, TranscoderPreset> presets)
    {
        var assetId = context.AssetId;
        var timeBasedPolicies = context.Asset.ImageDeliveryChannels
            .GetTimebasedChannel(true)!.DeliveryChannelPolicy.AsTimebasedPresets();
        
        var outputs = new List<CreateJobOutput>();

        foreach (var timeBasedPolicy in timeBasedPolicies)
        {
            var mediaType = context.Asset.MediaType;
            
            if (!settings.DeliveryChannelMappings.TryGetValue(timeBasedPolicy, out var mappedPresetName))
            {
                logger.LogWarning("Unable to find preset {TimeBasedPolicy} in the allowed mappings", timeBasedPolicy);
                continue;
            }

            var parsedTimeBasedPolicy = new TimeBasedPolicy(timeBasedPolicy);
            
            var destinationPath = TranscoderTemplates.ProcessPreset(
                mediaType, assetId, jobId, parsedTimeBasedPolicy.Extension);
            
            if (!presets.TryGetValue(mappedPresetName, out var transcoderPreset))
            {
                logger.LogWarning("Mapping for preset '{PresetName}' not found!", mappedPresetName);
                continue;
            }

            outputs.Add(new CreateJobOutput
            {
                PresetId = transcoderPreset.Id,
                Key = destinationPath,
            });

            logger.LogDebug("Asset {AssetId} will be output to '{Destination}' for '{TechnicalDetail}'", assetId,
                destinationPath, timeBasedPolicy);
        }

        return outputs;
    }
}
