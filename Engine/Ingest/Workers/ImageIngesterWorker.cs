using System.Diagnostics;
using DLCS.Model.Assets;
using DLCS.Model.Customers;
using DLCS.Model.Messaging;
using DLCS.Model.Templates;
using Engine.Settings;
using Microsoft.Extensions.Options;

namespace Engine.Ingest.Workers;

public class ImageIngesterWorker : IAssetIngesterWorker
{
    private readonly EngineSettings engineSettings;
    private readonly ILogger<ImageIngesterWorker> logger;
    private readonly IAssetMover assetMover;

    public ImageIngesterWorker(
        IOptionsMonitor<EngineSettings> engineOptions,
        AssetMoverResolver assetMoverResolver,
        ILogger<ImageIngesterWorker> logger)

    {
        assetMover = assetMoverResolver(AssetMoveType.Disk);
        engineSettings = engineOptions.CurrentValue;
        this.logger = logger;
    }

    /// <summary>
    /// <see cref="IAssetIngesterWorker"/> for ingesting Image assets (Family = I).
    /// </summary>
    public async Task<IngestResult> Ingest(IngestAssetRequest ingestAssetRequest,
        CustomerOriginStrategy customerOriginStrategy, CancellationToken cancellationToken = default)
    {
        try
        {
            var sourceTemplate = GetSourceTemplate(ingestAssetRequest.Asset);
            var stopwatch = Stopwatch.StartNew();
            var assetOnDisk = await assetMover.CopyAsset(
                ingestAssetRequest.Asset, 
                sourceTemplate, 
                !SkipStoragePolicyCheck(ingestAssetRequest.Asset.Customer),
                customerOriginStrategy,
                cancellationToken);
            stopwatch.Stop();
            logger.LogDebug("Copied image asset {AssetId} in {Elapsed}ms", stopwatch.ElapsedMilliseconds,
                ingestAssetRequest.Asset.Id);
            
            if (assetOnDisk.FileExceedsAllowance)
            {
                ingestAssetRequest.Asset.Error = "StoragePolicy size limit exceeded";
                // await imageCompletion.CompleteIngestion(context, false, sourceTemplate);
                return IngestResult.Failed;
            }
            
            var context = new IngestionContext(ingestAssetRequest.Asset, assetOnDisk);
            
            /*var ingestSuccess = await imageProcessor.ProcessImage(context);

            var completionSuccess = await imageCompletion.CompleteIngestion(context, ingestSuccess, sourceTemplate);

            return ingestSuccess && completionSuccess ? IngestResult.Success : IngestResult.Failed;*/

            return IngestResult.Unknown;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error ingesting image {AssetId}", ingestAssetRequest.Asset.Id);
            return IngestResult.Failed;
        }
    }

    private string GetSourceTemplate(Asset asset)
    {
        var imageIngest = engineSettings.ImageIngest;
        var root = imageIngest.GetRoot();
            
        // source is the main folder for storing
        var assetId = asset.GetAssetId();
        var source = TemplatedFolders.GenerateFolderTemplate(imageIngest.SourceTemplate, assetId, root: root);
            
        // dest is the folder where image-processor will copy output
        var dest = TemplatedFolders.GenerateFolderTemplate(imageIngest.DestinationTemplate, assetId, root: root);
            
        // thumb is the folder where generated thumbnails will be output
        var thumb = TemplatedFolders.GenerateFolderTemplate(imageIngest.ThumbsTemplate, assetId, root: root);

        Directory.CreateDirectory(dest);
        Directory.CreateDirectory(thumb);
        Directory.CreateDirectory(source);

        return source;
    }
    
    private bool SkipStoragePolicyCheck(int customerId)
    {
        var customerSpecific = engineSettings.GetCustomerSettings(customerId);
        return customerSpecific.NoStoragePolicyCheck;
    }
}