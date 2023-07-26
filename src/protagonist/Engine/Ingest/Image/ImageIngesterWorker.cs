using System.Diagnostics;
using DLCS.Model.Customers;
using Engine.Ingest.Image.Completion;
using Engine.Ingest.Persistence;
using Engine.Settings;
using Microsoft.Extensions.Options;

namespace Engine.Ingest.Image;

/// <summary>
/// Class that contains logic for ingesting image asset - copies from origin and sends to image processor
/// </summary>
public class ImageIngesterWorker : IAssetIngesterWorker, IAssetIngesterPostProcess
{
    private readonly EngineSettings engineSettings;
    private readonly IImageProcessor imageProcessor;
    private readonly IAssetIngestorSizeCheck assetIngestorSizeCheck;
    private readonly IImageIngestPostProcessing imageCompletion;
    private readonly ILogger<ImageIngesterWorker> logger;
    private readonly IAssetToDisk assetToDisk;

    public ImageIngesterWorker(
        IAssetToDisk assetToDisk,
        IImageProcessor imageProcessor,
        IOptionsMonitor<EngineSettings> engineOptions,
        IAssetIngestorSizeCheck assetIngestorSizeCheck,
        IImageIngestPostProcessing imageCompletion,
        ILogger<ImageIngesterWorker> logger)
    {
        this.assetToDisk = assetToDisk;
        engineSettings = engineOptions.CurrentValue;
        this.imageProcessor = imageProcessor;
        this.assetIngestorSizeCheck = assetIngestorSizeCheck;
        this.imageCompletion = imageCompletion;
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
        var sourceTemplate = ImageIngestionHelpers.GetSourceFolder(asset, engineSettings);

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

            ingestionContext.UpdateMediaTypeIfRequired();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error ingesting image {AssetId}", asset.Id);
            ingestSuccess = false;
            asset.Error = ex.Message;
        }

        return ingestSuccess ? IngestResultStatus.Success : IngestResultStatus.Failed;
    }

    public Task PostIngest(IngestionContext ingestionContext, bool ingestSuccessful)
        => imageCompletion.CompleteIngestion(ingestionContext, ingestSuccessful);
}