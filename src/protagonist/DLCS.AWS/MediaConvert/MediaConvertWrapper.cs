using System.Net;
using Amazon.MediaConvert;
using Amazon.MediaConvert.Model;
using DLCS.AWS.MediaConvert.Models;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.AWS.Settings;
using DLCS.AWS.Transcoding;
using DLCS.AWS.Transcoding.Models.Job;
using DLCS.AWS.Transcoding.Models.Request;
using DLCS.Core.Caching;
using DLCS.Core.Collections;
using DLCS.Core.Streams;
using DLCS.Core.Types;
using LazyCache;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using CreateJobRequest = Amazon.MediaConvert.Model.CreateJobRequest;
using CreateJobResponse = DLCS.AWS.Transcoding.Models.Request.CreateJobResponse;
using TimeSpan = System.TimeSpan;

namespace DLCS.AWS.MediaConvert;

public class MediaConvertWrapper(
    IAmazonMediaConvert mediaConvert,
    IAppCache cache,
    IBucketWriter bucketWriter,
    IBucketReader bucketReader,
    IStorageKeyGenerator storageKeyGenerator,
    IOptionsMonitor<CacheSettings> cacheSettings,
    IOptionsMonitor<AWSSettings> awsSettings,
    MediaConvertResponseConverter  responseConverter,
    ILogger<MediaConvertWrapper> logger)
    : ITranscoderWrapper
{
    private readonly JsonSerializerSettings serializerSettings = new()
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver()
    };
    
    public async Task<string?> GetPipelineId(string pipelineName, CancellationToken token = default)
    {
        const string nullObject = "__notfound__";
        const string queueKey = "MediaTranscode:QueueId";

        var currentCacheSettings = cacheSettings.CurrentValue;

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


            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(currentCacheSettings.GetTtl(CacheDuration.Short));
            entry.Priority = CacheItemPriority.Low;
            return nullObject;
        }, currentCacheSettings.GetMemoryCacheOptions(CacheDuration.Long, priority: CacheItemPriority.Low));

        return pipelineId == nullObject ? null : pipelineId;
    }

    public async Task<CreateJobResponse> CreateJob(string inputKey, string pipelineId, IJobOutput output,
        Dictionary<string, string> jobMetadata, CancellationToken token = default)
    {
        var outputGroup = (MediaConvertJobGroup)output;
        var createJobRequest = new CreateJobRequest
        {
            Queue = pipelineId,
            Role = awsSettings.CurrentValue.Transcode.RoleArn,
            UserMetadata = jobMetadata,
            Settings = new JobSettings
            {
                FollowSource = 1,
                TimecodeConfig = new TimecodeConfig { Source = TimecodeSource.ZEROBASED },
                Inputs =
                [
                    new Input
                    {
                        FileInput = inputKey,
                        AudioSelectors = new Dictionary<string, AudioSelector>
                        {
                            ["Audio Selector 1"] = new()
                            {
                                DefaultSelection = AudioDefaultSelection.DEFAULT
                            }
                        },
                        TimecodeSource = InputTimecodeSource.ZEROBASED
                    }
                ],
                OutputGroups =
                [
                    new OutputGroup
                    {
                        OutputGroupSettings = new OutputGroupSettings
                        {
                            FileGroupSettings = new FileGroupSettings
                            {
                                Destination = outputGroup.Destination.GetS3Uri().ToString(),
                            },
                            Type = OutputGroupType.FILE_GROUP_SETTINGS,
                        },
                        Outputs = outputGroup.Outputs.Select(CreateOutput).ToList()
                    }
                ],
            }
        };
        
        var response = await mediaConvert.CreateJobAsync(createJobRequest, token);
        logger.LogDebug("Created job {JobId} with {OutputCount} outputs. Status:{StatusCode}", response.Job.Id,
            response.HttpStatusCode, outputGroup.Outputs.Count);
        return new CreateJobResponse(response.Job.Id, response.HttpStatusCode);
    }
    
    private static Output CreateOutput(MediaConvertOutput mediaConvertOutput, int index) =>
        new()
        {
            Preset = mediaConvertOutput.Preset,
            Extension = mediaConvertOutput.Extension,
            NameModifier = mediaConvertOutput.NameModifier ?? $"_{index}",
        };

    public Task PersistJobId(AssetId assetId, string transcoderJobId, CancellationToken cancellationToken)
    {
        var metadataKey = storageKeyGenerator.GetTimebasedMetadataLocation(assetId);
        logger.LogTrace("Writing timebased metadata for {Asset} to {MetadataKey}", assetId, metadataKey);

        var metadataContent = JsonConvert.SerializeObject(new JobMetadata(transcoderJobId), serializerSettings);
        return bucketWriter.WriteToBucket(metadataKey, metadataContent, "application/json", cancellationToken);
    }

    public async Task<TranscoderJob?> GetTranscoderJob(AssetId assetId, CancellationToken cancellationToken)
    {
        var metadataKey = storageKeyGenerator.GetTimebasedMetadataLocation(assetId);
        var metadataObject = await bucketReader.GetObjectFromBucket(metadataKey, cancellationToken);
        
        var jobMetadata = await TryGetJobMetadata(assetId, metadataObject);
        if (jobMetadata == null) return null;

        var transcoderJob = await GetTranscoderJobInternal(assetId, jobMetadata.JobId, cancellationToken);
        return transcoderJob;
    }

    public async Task<TranscoderJob?> GetTranscoderJob(AssetId assetId, string jobId, CancellationToken cancellationToken)
    {
        var transcoderJob = await GetTranscoderJobInternal(assetId, jobId, cancellationToken);
        var assetIdForJob = transcoderJob.GetAssetId();
        if (assetIdForJob == null || assetIdForJob != assetId)
        {
            logger.LogWarning("Fetched jobId {JobId} for Asset {AssetId} but UserMetadata has Asset {JobAssetId}",
                jobId, assetId, assetIdForJob);
            return null;
        }
        
        return transcoderJob;
    }

    private async Task<TranscoderJob> GetTranscoderJobInternal(AssetId assetId, string jobId, CancellationToken cancellationToken)
    {
        var jobInfo = await mediaConvert.GetJobAsync(new GetJobRequest { Id = jobId }, cancellationToken);
        var transcoderJob = responseConverter.Create(jobInfo.Job, assetId);
        return transcoderJob;
    }

    private async Task<JobMetadata?> TryGetJobMetadata(AssetId assetId, ObjectFromBucket metadataObject)
    {
        if (metadataObject.Stream.IsNull())
        {
            logger.LogTrace("Unable to find metadata for AssetId {AssetId}", assetId);
            return null;
        }

        if (metadataObject.Headers.ContentType is "application/xml")
        {
            logger.LogDebug("Metadata for AssetId {AssetId} is XML, so transcoded by ElasticTranscoder", assetId);
            return null;
        }

        var jobMetadata = await metadataObject.DeserializeFromJson<JobMetadata>();
        if (jobMetadata == null || jobMetadata.JobId.IsNullOrEmpty())
        {
            logger.LogDebug("Unable to read jobId from metadata for AssetId {AssetId}", assetId);
            return null;
        }

        return jobMetadata;
    }

    /// <summary>
    /// Placeholder for MediaConvert transcoding job that we store in S3 for fetching full details.
    /// </summary>
    /// <remarks>
    /// TranscodingService is not used currently but adding for ease of identification should we need to switch to a
    /// different transcoding service in future
    /// </remarks>
    private record JobMetadata(string JobId, string TranscodingService = "MediaConvert");
}
