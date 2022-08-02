using Amazon.ElasticTranscoder.Model;
using DLCS.AWS.ElasticTranscoder;
using DLCS.AWS.S3;
using DLCS.Core.Guard;
using DLCS.Core.Types;
using Engine.Settings;
using Microsoft.Extensions.Options;

namespace Engine.Ingest.Timebased;

public class ElasticTranscoder : IMediaTranscoder
{
    private readonly IOptionsMonitor<EngineSettings> engineSettings;
    private readonly IElasticTranscoderWrapper elasticTranscoderWrapper;
    private readonly IBucketWriter bucketWriter;
    private readonly IStorageKeyGenerator storageKeyGenerator;
    private readonly ILogger<ElasticTranscoder> logger;

    public ElasticTranscoder(
        IElasticTranscoderWrapper elasticTranscoderWrapper,
        IBucketWriter bucketWriter,
        IStorageKeyGenerator storageKeyGenerator,
        IOptionsMonitor<EngineSettings> engineSettings,
        ILogger<ElasticTranscoder> logger)
    {
        this.elasticTranscoderWrapper = elasticTranscoderWrapper;
        this.bucketWriter = bucketWriter;
        this.engineSettings = engineSettings;
        engineSettings.CurrentValue.TimebasedIngest.ThrowIfNull(nameof(engineSettings.CurrentValue.TimebasedIngest));
        this.logger = logger;
        this.storageKeyGenerator = storageKeyGenerator;
    }
    
    public async Task<bool> InitiateTranscodeOperation(IngestionContext context, CancellationToken token = default)
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

        var elasticTranscoderJob = await elasticTranscoderWrapper.CreateJob(context.AssetId,
            context.AssetFromOrigin.Location, pipelineId, outputs, jobId, token);
        
        var statusCode = (int) elasticTranscoderJob.HttpStatusCode;

        logger.LogDebug("Created ET job {ETJobId}, got response {StatusCode}", elasticTranscoderJob.Job?.Id,
            elasticTranscoderJob.HttpStatusCode);

        if (statusCode is not (>= 200 and < 300))
        {
            context.Asset.Error = $"Create ElasticTranscoder job failed with status {statusCode}";
            return false;
        }

        await WriteMetadataObject(context.AssetId, elasticTranscoderJob.Job.Id, token);
        return true;
    }

    private List<CreateJobOutput> GetJobOutputs(IngestionContext context, string jobId,
        TimebasedIngestSettings settings, Dictionary<string, string> presets)
    {
        var asset = context.Asset;
        var assetId = context.AssetId;
        var technicalDetails = asset.FullImageOptimisationPolicy.TechnicalDetails;
        var outputs = new List<CreateJobOutput>(technicalDetails.Length);

        foreach (var technicalDetail in technicalDetails)
        {
            // TODO - this? Or Asset.MediaType
            var mediaType = context.AssetFromOrigin.ContentType;
            var (destinationPath, presetName) =
                TranscoderTemplates.ProcessPreset(mediaType, assetId, technicalDetail, jobId);

            // TODO - handle empty path/presetname
            var mappedPresetName = settings.TranscoderMappings.TryGetValue(presetName, out var mappedName)
                ? mappedName
                : presetName;

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
                destinationPath, technicalDetail);
        }

        return outputs;
    }

    private async Task WriteMetadataObject(AssetId assetId, string elasticTranscoderJobId,
        CancellationToken cancellationToken)
    {
        // NOTE - this is XML to copy Deliverator implementation
        var metadataKey = storageKeyGenerator.GetTimebasedMetadataLocation(assetId);
        var metadataContent =
            $"<JobInProgress><ElasticTranscoderJob>{elasticTranscoderJobId}</ElasticTranscoderJob></JobInProgress>";
        
        logger.LogDebug("Writing timebased metadata to {MetadataKey}", metadataKey);

        await bucketWriter.WriteToBucket(metadataKey, metadataContent, "application/xml", cancellationToken);
    }
}