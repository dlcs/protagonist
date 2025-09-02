using System.Net;
using Amazon.ElasticTranscoder.Model;
using Amazon.MediaConvert;
using Amazon.MediaConvert.Model;
using DLCS.AWS.Transcoding;
using DLCS.AWS.Transcoding.Models.Job;
using DLCS.AWS.Transcoding.Models.Request;
using DLCS.Core.Caching;
using DLCS.Core.Types;
using LazyCache;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using CreateJobResponse = DLCS.AWS.Transcoding.Models.Request.CreateJobResponse;
using TimeSpan = System.TimeSpan;

namespace DLCS.AWS.MediaConvert;

public class MediaConvertWrapper : ITranscoderWrapper
{
    private readonly IAmazonMediaConvert mediaConvert;
    private readonly IAppCache cache;
    private readonly CacheSettings cacheSettings;
    private readonly ILogger<MediaConvertWrapper> logger;
    
    public MediaConvertWrapper(
        IAmazonMediaConvert mediaConvert, 
        IAppCache cache,
        IOptions<CacheSettings> cacheSettings,
        ILogger<MediaConvertWrapper> logger)
    {
        this.mediaConvert = mediaConvert;
        this.cache = cache;
        this.cacheSettings = cacheSettings.Value;
        this.logger = logger;
    }

    public async Task<string?> GetPipelineId(string pipelineName, CancellationToken token = default)
    {
        const string nullObject = "__notfound__";
        const string queueKey = "MediaTranscode:QueueId";

        var pipelineId = await cache.GetOrAddAsync(queueKey, async entry =>
        {
            var request = new GetQueueRequest { Name = pipelineName };
            var response = await mediaConvert.GetQueueAsync(request, token);

            if (response.HttpStatusCode == HttpStatusCode.OK)
            {
                var queue = response.Queue;
                logger.LogTrace("Found queue {QueueArn} for {QueueName}. Price {Whatever}",
                    queue.Arn, pipelineName, queue.ConcurrentJobs);
                return queue.Arn;
            }

            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(cacheSettings.GetTtl(CacheDuration.Short));
            entry.Priority = CacheItemPriority.Low;
            return nullObject;
        }, cacheSettings.GetMemoryCacheOptions(CacheDuration.Long, priority: CacheItemPriority.Low));

        return pipelineId == nullObject ? null : pipelineId;
    }

    public Task<CreateJobResponse> CreateJob(string inputKey, string pipelineId, IJobOutput output, Dictionary<string, string> jobMetadata,
        CancellationToken token = default)
    {
        throw new NotImplementedException();
    }

    public Task<CreateJobResponse> CreateJob(string inputKey, string pipelineId, List<IJobOutput> outputs, Dictionary<string, string> jobMetadata,
        CancellationToken token = default)
    {
        throw new NotImplementedException();
    }

    public Task<Amazon.ElasticTranscoder.Model.CreateJobResponse> CreateJob(string inputKey, string pipelineId, List<CreateJobOutput> outputs, Dictionary<string, string> jobMetadata,
        CancellationToken token = default)
    {
        throw new NotImplementedException();
    }

    public Task PersistJobId(AssetId assetId, string transcoderJobId, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<TranscoderJob?> GetTranscoderJob(AssetId assetId, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
