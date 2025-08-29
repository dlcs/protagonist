using System.Xml.Linq;
using Amazon.ElasticTranscoder;
using Amazon.ElasticTranscoder.Model;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.AWS.Transcoding;
using DLCS.AWS.Transcoding.Models.Job;
using DLCS.Core.Caching;
using DLCS.Core.Streams;
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
[Obsolete("ElasticTranscoder is being replaced by MediaConvert")]
public class ElasticTranscoderWrapper : ITranscoderWrapper
{
    private readonly IAmazonElasticTranscoder elasticTranscoder;
    private readonly IAppCache cache;
    private readonly CacheSettings cacheSettings;
    private readonly IBucketWriter bucketWriter;
    private readonly IBucketReader bucketReader;
    private readonly IStorageKeyGenerator storageKeyGenerator;
    private readonly ILogger<ElasticTranscoderWrapper> logger;

    public ElasticTranscoderWrapper(IAmazonElasticTranscoder elasticTranscoder,
        IAppCache cache, 
        IBucketWriter bucketWriter,
        IBucketReader bucketReader,
        IStorageKeyGenerator storageKeyGenerator,
        IOptions<CacheSettings> cacheSettings,
        ILogger<ElasticTranscoderWrapper> logger)
    {
        this.elasticTranscoder = elasticTranscoder;
        this.cache = cache;
        this.cacheSettings = cacheSettings.Value;
        this.logger = logger;
        this.bucketReader = bucketReader;
        this.bucketWriter = bucketWriter;
        this.storageKeyGenerator = storageKeyGenerator;
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

    public Task<CreateJobResponse> CreateJob(string inputKey, string pipelineId, List<CreateJobOutput> outputs,
        Dictionary<string, string> jobMetadata, CancellationToken token)
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
            UserMetadata = jobMetadata,
            Outputs = outputs
        };
        return elasticTranscoder.CreateJobAsync(createJobRequest, token);
    }

    public async Task PersistJobId(AssetId assetId, string transcoderJobId, CancellationToken cancellationToken)
    {
        // NOTE - this is XML to copy Deliverator implementation
        var metadataKey = storageKeyGenerator.GetTimebasedMetadataLocation(assetId);
        var metadataContent =
            $"<JobInProgress><ElasticTranscoderJob>{transcoderJobId}</ElasticTranscoderJob></JobInProgress>";
        
        logger.LogDebug("Writing timebased metadata for {Asset} to {MetadataKey}", assetId, metadataKey);

        await bucketWriter.WriteToBucket(metadataKey, metadataContent, "application/xml", cancellationToken);
    }

    public async Task<TranscoderJob?> GetTranscoderJob(AssetId assetId, CancellationToken cancellationToken)
    {
        var metadataKey = storageKeyGenerator.GetTimebasedMetadataLocation(assetId);
        var metadataStream = await bucketReader.GetObjectContentFromBucket(metadataKey, cancellationToken);
        if (metadataStream.IsNull()) return null;

        var jobId = await GetJobId(metadataStream!, assetId);
        if (string.IsNullOrEmpty(jobId)) return null;

        try
        {
            var readJobResponse =
                await elasticTranscoder.ReadJobAsync(new ReadJobRequest { Id = jobId }, cancellationToken);
            if (readJobResponse == null)
            {
                logger.LogInformation("ET job {JobId} for Asset {AssetId} not found", jobId, assetId);
                return null;
            }

            var transcoderJob = TranscoderJob.Create(readJobResponse.Job);
            return transcoderJob;
        }
        catch (AmazonElasticTranscoderException etEx)
        {
            logger.LogError(etEx, "AWS error reading job {JobId} for Asset {AssetId}", jobId, assetId);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "General exception parsing ET job {JobId} for Asset {AssetId}", jobId, assetId);
            throw;
        }
    }

    private async Task<string?> GetJobId(Stream metadataStream, AssetId assetId)
    {
        string xmlContent = string.Empty;
        try
        {
            using var reader = new StreamReader(metadataStream, true);
            xmlContent = await reader.ReadToEndAsync();
            var xmlDoc = XDocument.Parse(xmlContent);
            var jobId = xmlDoc.Descendants("ElasticTranscoderJob").Single().Value;
            
            if (string.IsNullOrEmpty(jobId))
            {
                logger.LogInformation("Unable to determine ET jobId for Asset {AssetId}, from xml {XmlContent}", assetId,
                    xmlContent);
                return null;
            }

            return jobId;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error parsing metadata xml {XmlContent} for Asset {AssetId}", xmlContent, assetId);
            return null;
        }
    }
}
