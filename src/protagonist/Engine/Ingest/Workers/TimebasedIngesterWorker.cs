using System.Diagnostics;
using DLCS.Model.Customers;
using DLCS.Model.Messaging;
using Engine.Ingest.Completion;
using Engine.Ingest.Timebased;
using Engine.Ingest.Workers.Persistence;
using Engine.Settings;
using Microsoft.Extensions.Options;

namespace Engine.Ingest.Workers;

/// <summary>
/// <see cref="IAssetIngesterWorker"/> responsible for ingesting timebased assets ('T' family).
/// </summary>
public class TimebasedIngesterWorker : IAssetIngesterWorker
{
    private readonly IAssetToS3 assetToS3;
    private readonly IMediaTranscoder mediaTranscoder;
    private readonly ITimebasedIngestorCompletion completion;
    private readonly EngineSettings engineSettings;
    private readonly ILogger<TimebasedIngesterWorker> logger;

    public TimebasedIngesterWorker(
        IAssetToS3 assetToS3,
        IOptionsMonitor<EngineSettings> engineOptions,
        IMediaTranscoder mediaTranscoder,
        ITimebasedIngestorCompletion completion,
        ILogger<TimebasedIngesterWorker> logger)
    {
        this.mediaTranscoder = mediaTranscoder;
        this.completion = completion;
        this.assetToS3 = assetToS3;
        engineSettings = engineOptions.CurrentValue;
        this.logger = logger;
    }
    
    public async Task<IngestResult> Ingest(IngestAssetRequest ingestAssetRequest,
        CustomerOriginStrategy customerOriginStrategy, CancellationToken cancellationToken = default)
    {
        var context = new IngestionContext(ingestAssetRequest.Asset);
        
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var assetInBucket = await assetToS3.CopyAssetToTranscodeInput(ingestAssetRequest.Asset,
                !SkipStoragePolicyCheck(ingestAssetRequest.Asset.Customer),
                customerOriginStrategy, cancellationToken);
            stopwatch.Stop();
            logger.LogDebug("Copied timebased asset {AssetId} in {Elapsed}ms using {OriginStrategy}", 
                ingestAssetRequest.Asset.Id, stopwatch.ElapsedMilliseconds, customerOriginStrategy.Strategy);
            
            context.WithAssetFromOrigin(assetInBucket);

            if (assetInBucket.FileExceedsAllowance)
            {
                ingestAssetRequest.Asset.Error = "StoragePolicy size limit exceeded";
                await completion.CompleteAssetInDatabase(ingestAssetRequest.Asset,
                    cancellationToken: cancellationToken);
                return IngestResult.StorageLimitExceeded;
            }

            var success = await mediaTranscoder.InitiateTranscodeOperation(context, cancellationToken);
            if (success)
            {
                logger.LogDebug("Timebased asset {AssetId} successfully queued for processing", context.AssetId);
                return IngestResult.QueuedForProcessing;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error ingesting timebased asset {AssetId}", ingestAssetRequest.Asset.Id);
            context.Asset.Error = ex.Message;
        }
        
        try
        {
            await completion.CompleteAssetInDatabase(ingestAssetRequest.Asset, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            // TODO - mark Asset Error here? and call completion
            logger.LogError(ex, "Error completing {AssetId}", ingestAssetRequest.Asset.Id);
        }
        
        // If we reach here then it's failed, if successful then we would have aborted after initiating transcode
        logger.LogDebug("Failed to ingest timebased asset {AssetId}", context.AssetId);
        return IngestResult.Failed;
    }
    
    private bool SkipStoragePolicyCheck(int customerId)
    {
        var customerSpecific = engineSettings.GetCustomerSettings(customerId);
        return customerSpecific.NoStoragePolicyCheck;
    }
}