using System.Diagnostics;
using DLCS.Model.Customers;
using DLCS.Model.Messaging;
using Engine.Ingest.Timebased;
using Engine.Ingest.Workers.Persistence;
using Engine.Settings;
using Microsoft.Extensions.Options;

namespace Engine.Ingest.Workers;

/// <summary>
/// <see cref="IAssetIngesterWorker"/> responsible for ingesting timebased assets ('T' family)  
/// </summary>
public class TimebasedIngesterWorker : IAssetIngesterWorker
{
    private readonly AssetToS3 assetToS3;
    private readonly IMediaTranscoder mediaTranscoder;
    //private readonly ITimebasedIngestorCompletion completion;
    private readonly EngineSettings engineSettings;
    private readonly ILogger<TimebasedIngesterWorker> logger;
     
    
    public TimebasedIngesterWorker(
        AssetToS3 assetToS3,
        IOptionsMonitor<EngineSettings> engineOptions,
        IMediaTranscoder mediaTranscoder,
        //ITimebasedIngestorCompletion completion,
        ILogger<TimebasedIngesterWorker> logger)
    {
        this.mediaTranscoder = mediaTranscoder;
        //this.completion = completion;
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

            // TODO - if (assetOnDisk.FileExceedsAllowance)
            context.WithAssetFromOrigin(assetInBucket);
            
            var success = await mediaTranscoder.InitiateTranscodeOperation(context, cancellationToken);

            if (success)
            {
                return IngestResult.QueuedForProcessing;
            }
            
            // TODO - handle failure to queue for transcoding
            throw new NotImplementedException();
        }
        catch (Exception ex)
        {
            // TODO - set context.Asset.Error and save
            logger.LogError(ex, "Error ingesting timebased asset {AssetId}", ingestAssetRequest.Asset.Id);
            return IngestResult.Failed;
        }
    }
    
    private bool SkipStoragePolicyCheck(int customerId)
    {
        var customerSpecific = engineSettings.GetCustomerSettings(customerId);
        return customerSpecific.NoStoragePolicyCheck;
    }
}