using System.Diagnostics;
using DLCS.Model.Assets;
using DLCS.Model.Customers;
using DLCS.Model.Messaging;
using DLCS.Model.Templates;
using Engine.Ingest.Image.Completion;
using Engine.Ingest.Persistence;
using Engine.Settings;
using Microsoft.Extensions.Options;

namespace Engine.Ingest.Image;

public class ImageIngesterWorker : IAssetIngesterWorker
{
    private readonly EngineSettings engineSettings;
    private readonly IImageProcessor imageProcessor;
    private readonly IImageIngestorCompletion imageCompletion;
    private readonly ILogger<ImageIngesterWorker> logger;
    private readonly IAssetToDisk assetToDisk;

    public ImageIngesterWorker(
        IAssetToDisk assetToDisk,
        IImageProcessor imageProcessor,
        IImageIngestorCompletion imageCompletion,
        IOptionsMonitor<EngineSettings> engineOptions,
        ILogger<ImageIngesterWorker> logger)
    {
        this.assetToDisk = assetToDisk;
        engineSettings = engineOptions.CurrentValue;
        this.imageProcessor = imageProcessor;
        this.imageCompletion = imageCompletion;
        this.logger = logger;
    }

    /// <summary>
    /// <see cref="IAssetIngesterWorker"/> for ingesting Image assets (Family = I).
    /// </summary>
    public async Task<IngestResultStatus> Ingest(IngestAssetRequest ingestAssetRequest,
        CustomerOriginStrategy customerOriginStrategy, CancellationToken cancellationToken = default)
    {
        bool ingestSuccess;
        string? sourceTemplate = null;
        var context = new IngestionContext(ingestAssetRequest.Asset);
        
        try
        {
            sourceTemplate = GetSourceTemplate(ingestAssetRequest.Asset);
            var stopwatch = Stopwatch.StartNew();
            var assetOnDisk = await assetToDisk.CopyAssetToLocalDisk(
                ingestAssetRequest.Asset, 
                sourceTemplate, 
                !SkipStoragePolicyCheck(ingestAssetRequest.Asset.Customer),
                customerOriginStrategy,
                cancellationToken);
            stopwatch.Stop();
            logger.LogDebug("Copied image asset {AssetId} in {Elapsed}ms using {OriginStrategy}", 
                ingestAssetRequest.Asset.Id, stopwatch.ElapsedMilliseconds, customerOriginStrategy.Strategy);
            
            if (assetOnDisk.FileExceedsAllowance)
            {
                ingestAssetRequest.Asset.Error = "StoragePolicy size limit exceeded";
                await imageCompletion.CompleteIngestion(context, false, sourceTemplate);
                return IngestResultStatus.StorageLimitExceeded;
            }

            context.WithAssetFromOrigin(assetOnDisk);

            ingestSuccess = await imageProcessor.ProcessImage(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error ingesting image {AssetId}", ingestAssetRequest.Asset.Id);
            ingestSuccess = false;
            context.Asset.Error = ex.Message;
        }

        try
        {
            var completionSuccess = await imageCompletion.CompleteIngestion(context, ingestSuccess, sourceTemplate);
            return ingestSuccess && completionSuccess ? IngestResultStatus.Success : IngestResultStatus.Failed;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error completing {AssetId}", ingestAssetRequest.Asset.Id);
            return IngestResultStatus.Failed;
        }
    }

    private string GetSourceTemplate(Asset asset)
    {
        var imageIngest = engineSettings.ImageIngest;
        var root = imageIngest.GetRoot();
            
        // source is the main folder for storing downloaded image
        var assetId = asset.GetAssetId();
        var source = TemplatedFolders.GenerateFolderTemplate(imageIngest.SourceTemplate, assetId, root: root);
        return source;
    }
    
    private bool SkipStoragePolicyCheck(int customerId)
    {
        var customerSpecific = engineSettings.GetCustomerSettings(customerId);
        return customerSpecific.NoStoragePolicyCheck;
    }
}