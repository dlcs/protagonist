using Amazon.ElasticTranscoder;
using Amazon.ElasticTranscoder.Model;
using DLCS.AWS.S3.Models;
using DLCS.Core.Caching;
using DLCS.Core.Types;
using LazyCache;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TimeSpan = System.TimeSpan;

namespace DLCS.AWS.ElasticTranscoder;

/// <summary>
/// Thin wrapper around <see cref="IAmazonElasticTranscoder"/>, handles paging/caching etc
/// </summary>
public class ElasticTranscoderWrapper : IElasticTranscoderWrapper
{
    private readonly IAmazonElasticTranscoder elasticTranscoder;
    private readonly IAppCache cache;
    private readonly CacheSettings cacheSettings;
    private readonly ILogger<ElasticTranscoderWrapper> logger;

    public ElasticTranscoderWrapper(IAmazonElasticTranscoder elasticTranscoder,
        IAppCache cache,
        IOptions<CacheSettings> cacheSettings,
        ILogger<ElasticTranscoderWrapper> logger)
    {
        this.elasticTranscoder = elasticTranscoder;
        this.cache = cache;
        this.cacheSettings = cacheSettings.Value;
        this.logger = logger;
    }
    
    public Task<Dictionary<string, string>> GetPresetIdLookup(CancellationToken token)
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
    
    public async Task<string?> GetPipelineId(string pipelineName, CancellationToken token)
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

    public Task<CreateJobResponse> CreateJob(AssetId assetId, string inputKey, string pipelineId,
        List<CreateJobOutput> outputs, string jobId, CancellationToken token)
    {
        var objectInBucket = RegionalisedObjectInBucket.Parse(inputKey, true)!;

        var createJobRequest = new CreateJobRequest
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
                [UserMetadataKeys.DlcsId] = assetId.ToString(),
                [UserMetadataKeys.StartTime] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                [UserMetadataKeys.JobId] = jobId
            },
            Outputs = outputs
        };
        return elasticTranscoder.CreateJobAsync(createJobRequest, token);
    }
}