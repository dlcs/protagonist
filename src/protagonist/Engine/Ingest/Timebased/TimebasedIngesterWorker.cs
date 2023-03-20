using DLCS.AWS.S3;
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
                asset,
                !assetIngestorSizeCheck.CustomerHasNoStorageCheck(asset.Customer),
                customerOriginStrategy, cancellationToken);
            ingestionContext.WithAssetFromOrigin(assetInBucket);

            if (assetIngestorSizeCheck.DoesAssetFromOriginExceedAllowance(assetInBucket, asset))
            {
                return IngestResultStatus.StorageLimitExceeded;
            }

            var success = await mediaTranscoder.InitiateTranscodeOperation(ingestionContext, cancellationToken);
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
}