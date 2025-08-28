using DLCS.AWS.ElasticTranscoder;
using DLCS.AWS.S3;
using DLCS.AWS.Transcoding;
using DLCS.Core.Collections;
using DLCS.Model.Customers;
using Engine.Ingest.Persistence;
using Engine.Ingest.Timebased.Transcode;

namespace Engine.Ingest.Timebased;

/// <summary>
/// <see cref="IAssetIngesterWorker"/> responsible for ingesting timebased assets ('iiif-av' delivery channel).
/// </summary>
public class TimebasedIngesterWorker : IAssetIngesterWorker
{
    private readonly IAssetToS3 assetToS3;
    private readonly IMediaTranscoder mediaTranscoder;
    private readonly IStorageKeyGenerator storageKeyGenerator;
    private readonly IAssetIngestorSizeCheck assetIngestorSizeCheck;
    private readonly ILogger<TimebasedIngesterWorker> logger;

    public TimebasedIngesterWorker(
        IAssetToS3 assetToS3,
        IMediaTranscoder mediaTranscoder,
        IStorageKeyGenerator storageKeyGenerator,
        IAssetIngestorSizeCheck assetIngestorSizeCheck,
        ILogger<TimebasedIngesterWorker> logger)
    {
        this.mediaTranscoder = mediaTranscoder;
        this.storageKeyGenerator = storageKeyGenerator;
        this.assetIngestorSizeCheck = assetIngestorSizeCheck;
        this.assetToS3 = assetToS3;
        this.logger = logger;
    }
    
    public async Task<IngestResultStatus> Ingest(IngestionContext ingestionContext,
        CustomerOriginStrategy customerOriginStrategy, CancellationToken cancellationToken = default)
    {
        var asset = ingestionContext.Asset;
        
        try
        {
            var targetStorageLocation = storageKeyGenerator.GetTimebasedInputLocation(asset.Id);
            var assetInBucket = await assetToS3.CopyOriginToStorage(
                targetStorageLocation,
                ingestionContext,
                !assetIngestorSizeCheck.CustomerHasNoStorageCheck(asset.Customer),
                customerOriginStrategy, cancellationToken);
            ingestionContext.WithAssetFromOrigin(assetInBucket);

            var jobMetadata = GetJobMetadata(ingestionContext);
            var success =
                await mediaTranscoder.InitiateTranscodeOperation(ingestionContext, jobMetadata, cancellationToken);
            if (success)
            {
                logger.LogDebug("Timebased asset {AssetId} successfully queued for processing", asset.Id);
                return IngestResultStatus.QueuedForProcessing;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error ingesting timebased asset {AssetId}", asset.Id);
            asset.Error = ex.Message;
        }

        // If we reach here then it's failed, if successful then we would have aborted after initiating transcode
        logger.LogDebug("Failed to ingest timebased asset {AssetId}", asset.Id);
        return IngestResultStatus.Failed;
    }

    private Dictionary<string, string> GetJobMetadata(IngestionContext ingestionContext)
    {
        var jobMetadata = new Dictionary<string, string>
        {
            [TranscodeMetadataKeys.DlcsId] = ingestionContext.AssetId.ToString(),
            [TranscodeMetadataKeys.OriginSize] = (TryGetStoredOriginFileSize(ingestionContext) ?? 0).ToString(),
            [TranscodeMetadataKeys.BatchId] = ingestionContext.Asset.BatchAssets.IsNullOrEmpty()
                ? string.Empty
                : ingestionContext.Asset.BatchAssets.Single().BatchId.ToString()
        };
        
        return jobMetadata;
    }

    private long? TryGetStoredOriginFileSize(IngestionContext ingestionContext)
    {
        var originLocation = storageKeyGenerator.GetStoredOriginalLocation(ingestionContext.AssetId);
        return ingestionContext.StoredObjects.TryGetValue(originLocation, out var fileSize)
            ? fileSize
            : null;
    }
}
