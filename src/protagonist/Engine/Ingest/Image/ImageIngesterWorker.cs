using System.Diagnostics;
using DLCS.Core.FileSystem;
using DLCS.Model.Assets;
using DLCS.Model.Customers;
using DLCS.Model.Templates;
using Engine.Ingest.Image.Completion;
using Engine.Ingest.Persistence;
using Engine.Settings;
using Microsoft.Extensions.Options;

namespace Engine.Ingest.Image;

public class ImageIngesterWorker : IAssetIngesterWorker, IAssetIngesterPostProcess
{
    private readonly EngineSettings engineSettings;
    private readonly IImageProcessor imageProcessor;
    private readonly IOrchestratorClient orchestratorClient;
    private readonly IFileSystem fileSystem;
    private readonly IAssetIngestorSizeCheck assetIngestorSizeCheck;
    private readonly ILogger<ImageIngesterWorker> logger;
    private readonly IAssetToDisk assetToDisk;

    public ImageIngesterWorker(
        IAssetToDisk assetToDisk,
        IImageProcessor imageProcessor,
        IOrchestratorClient orchestratorClient,
        IFileSystem fileSystem,
        IOptionsMonitor<EngineSettings> engineOptions,
        IAssetIngestorSizeCheck assetIngestorSizeCheck,
        ILogger<ImageIngesterWorker> logger)
    {
        this.assetToDisk = assetToDisk;
        engineSettings = engineOptions.CurrentValue;
        this.imageProcessor = imageProcessor;
        this.orchestratorClient = orchestratorClient;
        this.fileSystem = fileSystem;
        this.assetIngestorSizeCheck = assetIngestorSizeCheck;
        this.logger = logger;
    }

    /// <summary>
    /// <see cref="IAssetIngesterWorker"/> for ingesting Image assets, with mediaType = image/* + channel iiif-img or
    /// thumbs
    /// </summary>
    public async Task<IngestResultStatus> Ingest(IngestionContext ingestionContext,
        CustomerOriginStrategy customerOriginStrategy, CancellationToken cancellationToken = default)
    {
        bool ingestSuccess;
        var asset = ingestionContext.Asset;
        var sourceTemplate = GetSourceTemplate(asset);

        try
        {
            var stopwatch = Stopwatch.StartNew();
            var assetOnDisk = await assetToDisk.CopyAssetToLocalDisk(
                asset,
                sourceTemplate,
                !assetIngestorSizeCheck.CustomerHasNoStorageCheck(asset.Customer),
                customerOriginStrategy,
                cancellationToken);
            stopwatch.Stop();
            logger.LogDebug("Copied image asset {AssetId} in {Elapsed}ms using {OriginStrategy}",
                asset.Id, stopwatch.ElapsedMilliseconds, customerOriginStrategy.Strategy);

            if (assetIngestorSizeCheck.DoesAssetFromOriginExceedAllowance(assetOnDisk, asset))
            {
                return IngestResultStatus.StorageLimitExceeded;
            }

            ingestionContext.WithAssetFromOrigin(assetOnDisk);

            ingestSuccess = await imageProcessor.ProcessImage(ingestionContext);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error ingesting image {AssetId}", asset.Id);
            ingestSuccess = false;
            asset.Error = ex.Message;
        }
        finally
        {
            fileSystem.DeleteDirectory(sourceTemplate, true);
        }

        return ingestSuccess ? IngestResultStatus.Success : IngestResultStatus.Failed;
    }

    private string GetSourceTemplate(Asset asset)
    {
        var imageIngest = engineSettings.ImageIngest;
        var root = imageIngest.GetRoot();
            
        // source is the main folder for storing downloaded image
        var assetId = asset.Id;
        var source = TemplatedFolders.GenerateFolderTemplate(imageIngest.SourceTemplate, assetId, root: root);
        return source;
    }

    public async Task PostIngest(IngestionContext ingestionContext, bool ingestSuccessful)
    {
        try
        {
            if (!ingestSuccessful) return;

            if (!ShouldOrchestrate(ingestionContext.Asset.Customer)) return;

            await orchestratorClient.TriggerOrchestration(ingestionContext.AssetId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error completing {AssetId}", ingestionContext.Asset.Id);
        }
    }

    private bool ShouldOrchestrate(int customerId)
    {
        var customerSpecific = engineSettings.GetCustomerSettings(customerId);
        return customerSpecific.OrchestrateImageAfterIngest ?? engineSettings.ImageIngest.OrchestrateImageAfterIngest;
    }
}