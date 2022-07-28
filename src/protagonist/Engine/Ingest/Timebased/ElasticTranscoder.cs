using Amazon.ElasticTranscoder;
using Amazon.ElasticTranscoder.Model;
using DLCS.AWS.S3.Models;
using DLCS.Core.Guard;
using DLCS.Repository.Caching;
using Engine.Settings;
using LazyCache;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using TimeSpan = System.TimeSpan;

namespace Engine.Ingest.Timebased;

public class ElasticTranscoder : IMediaTranscoder
{
    private readonly IAmazonElasticTranscoder elasticTranscoder;
    private readonly IAppCache cache;
    private readonly IOptionsMonitor<EngineSettings> engineSettings;
    private readonly CacheSettings cacheSettings;
    private readonly ILogger<ElasticTranscoder> logger;

    public ElasticTranscoder(IAmazonElasticTranscoder elasticTranscoder,
        IAppCache cache,
        IOptionsMonitor<EngineSettings> engineSettings,
        IOptions<CacheSettings> cacheOptions,
        ILogger<ElasticTranscoder> logger)
    {
        this.elasticTranscoder = elasticTranscoder;
        this.cache = cache;
        cacheSettings = cacheOptions.Value;
        this.engineSettings = engineSettings;
        engineSettings.CurrentValue.TimebasedIngest.ThrowIfNull(nameof(engineSettings.CurrentValue.TimebasedIngest));
        this.logger = logger;
    }
    
    public async Task<bool> InitiateTranscodeOperation(IngestionContext context, CancellationToken token = default)
    {
        var settings = engineSettings.CurrentValue.TimebasedIngest!;
        var pipelineId = await GetPipelineId(settings.PipelineName, token);

        if (string.IsNullOrEmpty(pipelineId))
        {
            logger.LogWarning("Pipeline Id not found for {PipelineName} to ingest {AssetId}", settings.PipelineName,
                context.AssetId);
            context.Asset.Error = "Could not find ElasticTranscoder pipeline";
            return false;
        }
        
        var presets = await GetPresetIdLookup(token);
        var outputs = GetJobOutputs(context, settings, presets);

        if (outputs.Count == 0)
        {
            context.Asset.Error = "Unable to generate ElasticTranscoder outputs";
            return false;
        }
        
        var request = CreateJobRequest(context, context.AssetFromOrigin.Location, pipelineId, outputs);
        
        var response = await elasticTranscoder.CreateJobAsync(request, token);
        
        var statusCode = (int) response.HttpStatusCode;
        var success = statusCode is >= 200 and < 300;

        if (!success)
        {
            context.Asset.Error = $"Create ElasticTranscoder job failed with status {statusCode}";
        }

        return success;
    }
    
    private async Task<string?> GetPipelineId(string pipelineName, CancellationToken token)
    {
        const string nullObject = "__notfound__";
        const string pipelinesKey = "MediaTranscode:PipelineId";

        var pipelineId = await cache.GetOrAddAsync(pipelinesKey, async entry =>
        {
            var response = new ListPipelinesResponse();

            do
            {
                var request = new ListPipelinesRequest { PageToken = response.NextPageToken };
                response = await elasticTranscoder.ListPipelinesAsync(request, token);

                var pipeline = response.Pipelines.FirstOrDefault(p => p.Name == pipelineName);
                if (pipeline != null)
                {
                    return pipeline.Id;
                }
                
            } while (response.NextPageToken != null);

            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(cacheSettings.GetTtl(CacheDuration.Short));
            entry.Priority = CacheItemPriority.Low;
            return nullObject;
        }, cacheSettings.GetMemoryCacheOptions(CacheDuration.Long, priority: CacheItemPriority.Low));

        return pipelineId == nullObject ? null : pipelineId;
    }
    
    private Task<Dictionary<string, string>> GetPresetIdLookup(CancellationToken token)
    {
        const string presetLookupKey = "MediaTranscode:Presets";

        return cache.GetOrAddAsync(presetLookupKey, async entry =>
        {
            var presets = new Dictionary<string, string>();
            var response = new ListPresetsResponse();
                
            do
            {
                var request = new ListPresetsRequest {PageToken = response.NextPageToken};
                response = await elasticTranscoder.ListPresetsAsync(request, token);

                foreach (var preset in response.Presets)
                {
                    presets.Add(preset.Name, preset.Id);
                }

            } while (response.NextPageToken != null);

            if (presets.Count == 0)
            {
                logger.LogInformation("No ElasticTranscoder presets found");
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(cacheSettings.GetTtl(CacheDuration.Short));
            }

            return presets;
        }, cacheSettings.GetMemoryCacheOptions(CacheDuration.Long, priority: CacheItemPriority.Low));
    }
    
    private List<CreateJobOutput> GetJobOutputs(IngestionContext context, TimebasedIngestSettings settings,
        Dictionary<string, string> presets)
    {
        var asset = context.Asset;
        var assetId = context.AssetId;
        var technicalDetails = asset.FullImageOptimisationPolicy.TechnicalDetails;
        var outputs = new List<CreateJobOutput>(technicalDetails.Length);
            
        foreach (var technicalDetail in technicalDetails)
        {
            // TODO - this? Or Asset.MediaType
            var mediaType = context.AssetFromOrigin.ContentType;
            var (destinationPath, presetName) = TranscoderTemplates.ProcessPreset(mediaType, assetId, technicalDetail);

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

    private static CreateJobRequest CreateJobRequest(IngestionContext context, string key, string pipelineId,
        List<CreateJobOutput> outputs)
    {
        var objectInBucket = RegionalisedObjectInBucket.Parse(key, true)!;

        return new CreateJobRequest
        {
            Input = new JobInput
            {
                AspectRatio = "auto",
                Container = "auto",
                FrameRate = "auto",
                Interlaced = "auto",
                Resolution = "auto",
                Key = objectInBucket.Key,
            },
            PipelineId = pipelineId,
            UserMetadata = new Dictionary<string, string>
            {
                [UserMetadataKeys.DlcsId] = context.AssetId.ToString(),
                [UserMetadataKeys.StartTime] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                [UserMetadataKeys.JobId] = Guid.NewGuid().ToString(), // Is this useful? 
            },
            Outputs = outputs
        };
    }
}