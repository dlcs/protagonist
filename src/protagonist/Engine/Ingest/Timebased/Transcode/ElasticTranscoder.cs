using System.Text.Json;
using Amazon.ElasticTranscoder.Model;
using DLCS.AWS.ElasticTranscoder;
using DLCS.Core.Guard;
using DLCS.Model.Assets;
using Engine.Settings;
using Microsoft.Extensions.Options;

namespace Engine.Ingest.Timebased.Transcode;

public class ElasticTranscoder : IMediaTranscoder
{
    private readonly IOptionsMonitor<EngineSettings> engineSettings;
    private readonly IElasticTranscoderWrapper elasticTranscoderWrapper;
    private readonly ILogger<ElasticTranscoder> logger;

    public ElasticTranscoder(
        IElasticTranscoderWrapper elasticTranscoderWrapper,
        IOptionsMonitor<EngineSettings> engineSettings,
        ILogger<ElasticTranscoder> logger)
    {
        this.elasticTranscoderWrapper = elasticTranscoderWrapper;
        this.engineSettings = engineSettings;
        engineSettings.CurrentValue.TimebasedIngest.ThrowIfNull(nameof(engineSettings.CurrentValue.TimebasedIngest));
        this.logger = logger;
    }

    public async Task<bool> InitiateTranscodeOperation(IngestionContext context, Dictionary<string ,string> jobMetadata,
        CancellationToken token = default)
    {
        var settings = engineSettings.CurrentValue.TimebasedIngest!;
        var pipelineId = await elasticTranscoderWrapper.GetPipelineId(settings.PipelineName, token);

        if (string.IsNullOrEmpty(pipelineId))
        {
            logger.LogWarning("Pipeline Id not found for {PipelineName} to ingest {AssetId}", settings.PipelineName,
                context.AssetId);
            context.Asset.Error = "Could not find ElasticTranscoder pipeline";
            return false;
        }

        var presets = await elasticTranscoderWrapper.GetPresetIdLookup(token);

        // Create a guid to uniquely identify this job - this is added to ET output path to avoid overwriting by
        // separate jobs  
        var jobId = Guid.NewGuid().ToString();
        var outputs = GetJobOutputs(context, jobId, settings, presets);

        if (outputs.Count == 0)
        {
            context.Asset.Error = "Unable to generate ElasticTranscoder outputs";
            return false;
        }

        jobMetadata[UserMetadataKeys.JobId] = jobId;
        jobMetadata[UserMetadataKeys.StartTime] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        
        var elasticTranscoderJob = await elasticTranscoderWrapper.CreateJob(context.AssetFromOrigin.Location,
            pipelineId, outputs, jobMetadata, token);

        var statusCode = (int)elasticTranscoderJob.HttpStatusCode;

        logger.LogDebug("Created ET job {ETJobId}, got response {StatusCode}", elasticTranscoderJob.Job?.Id,
            elasticTranscoderJob.HttpStatusCode);

        if (statusCode is not (>= 200 and < 300))
        {
            context.Asset.Error = $"Create ElasticTranscoder job failed with status {statusCode}";
            return false;
        }

        await elasticTranscoderWrapper.PersistJobId(context.AssetId, elasticTranscoderJob.Job.Id, token);
        return true;
    }

    private List<CreateJobOutput> GetJobOutputs(IngestionContext context, string jobId,
        TimebasedIngestSettings settings, Dictionary<string, string> presets)
    {
        var asset = context.Asset;
        var assetId = context.AssetId;
        var timeBasedPolicies = asset.ImageDeliveryChannels.Where(i => i.Channel == AssetDeliveryChannels.Timebased)
            .Select(x => JsonSerializer.Deserialize<List<string>>(x.DeliveryChannelPolicy.PolicyData))
            .SelectMany(i => i).ToList();
        var outputs = new List<CreateJobOutput>();

        foreach (var timeBasedPolicy in timeBasedPolicies)
        {
            var mediaType = context.Asset.MediaType;
            
            // TODO - handle empty path/presetname
            if (!settings.DeliveryChannelMappings.TryGetValue(timeBasedPolicy, out var mappedPresetName))
            {
                logger.LogWarning("Unable to find preset {TimeBasedPolicy} in the allowed mappings", timeBasedPolicy);
                continue;
            }
            
            var (destinationPath, presetName) =
                TranscoderTemplates.ProcessPreset(mediaType, assetId, mappedPresetName, jobId, timeBasedPolicy.Split('-')[1]);

            // TODO - handle not found
            if (!presets.TryGetValue(mappedPresetName, out var presetId))
            {
                logger.LogWarning("Mapping for preset '{PresetName}' not found!", presetName);
                continue;
            }

            outputs.Add(new CreateJobOutput
            {
                PresetId = presetId,
                Key = destinationPath,
            });

            logger.LogDebug("Asset {AssetId} will be output to '{Destination}' for '{TechnicalDetail}'", assetId,
                destinationPath, timeBasedPolicy);
        }

        return outputs;
    }
}