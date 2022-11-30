using System.Diagnostics;
using DLCS.Model.Customers;
using Engine.Ingest.Persistence;
using Engine.Ingest.Timebased.Transcode;
using Engine.Settings;
using Microsoft.Extensions.Options;

namespace Engine.Ingest.Timebased;

/// <summary>
/// <see cref="IAssetIngesterWorker"/> responsible for ingesting timebased assets ('T' family).
/// </summary>
public class TimebasedIngesterWorker : IAssetIngesterWorker
{
    private readonly IAssetToS3 assetToS3;
    private readonly IMediaTranscoder mediaTranscoder;
    private readonly EngineSettings engineSettings;
    private readonly ILogger<TimebasedIngesterWorker> logger;

    public TimebasedIngesterWorker(
        IAssetToS3 assetToS3,
        IOptionsMonitor<EngineSettings> engineOptions,
        IMediaTranscoder mediaTranscoder,
        ILogger<TimebasedIngesterWorker> logger)
    {
        this.mediaTranscoder = mediaTranscoder;
        this.assetToS3 = assetToS3;
        engineSettings = engineOptions.CurrentValue;
        this.logger = logger;
    }
    
    public async Task<IngestResultStatus> Ingest(IngestionContext ingestionContext,
        CustomerOriginStrategy customerOriginStrategy, CancellationToken cancellationToken = default)
    {
        var asset = ingestionContext.Asset;
        
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var assetInBucket = await assetToS3.CopyAssetToTranscodeInput(asset,
                !SkipStoragePolicyCheck(asset.Customer),
                customerOriginStrategy, cancellationToken);
            stopwatch.Stop();
            logger.LogDebug("Copied timebased asset {AssetId} in {Elapsed}ms using {OriginStrategy}", 
                asset.Id, stopwatch.ElapsedMilliseconds, customerOriginStrategy.Strategy);
            
            ingestionContext.WithAssetFromOrigin(assetInBucket);

            if (assetInBucket.FileExceedsAllowance)
            {
                asset.Error = "StoragePolicy size limit exceeded";
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
    
    private bool SkipStoragePolicyCheck(int customerId)
    {
        var customerSpecific = engineSettings.GetCustomerSettings(customerId);
        return customerSpecific.NoStoragePolicyCheck;
    }
}