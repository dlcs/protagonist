using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.Core;
using DLCS.Core.FileSystem;
using DLCS.Core.Guard;
using DLCS.Core.Strings;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Policies;
using DLCS.Model.Templates;
using DLCS.Web.Requests;
using Engine.Ingest.Persistence;
using Engine.Settings;
using Microsoft.Extensions.Options;

namespace Engine.Ingest.Image.Appetiser;

/// <summary>
/// Derivative generator using Appetiser for generating resources
/// </summary>
public class AppetiserClient : IImageProcessor
{
    private readonly HttpClient httpClient;
    private readonly EngineSettings engineSettings;
    private readonly ILogger<AppetiserClient> logger;
    private readonly IBucketWriter bucketWriter;
    private readonly IStorageKeyGenerator storageKeyGenerator;
    private readonly IThumbCreator thumbCreator;
    private readonly IFileSystem fileSystem;

    public AppetiserClient(
        HttpClient httpClient,
        IBucketWriter bucketWriter,
        IStorageKeyGenerator storageKeyGenerator,
        IThumbCreator thumbCreator,
        IFileSystem fileSystem,
        IOptionsMonitor<EngineSettings> engineOptionsMonitor,
        ILogger<AppetiserClient> logger)
    {
        this.httpClient = httpClient;
        this.bucketWriter = bucketWriter;
        this.storageKeyGenerator = storageKeyGenerator;
        this.thumbCreator = thumbCreator;
        this.fileSystem = fileSystem;
        engineSettings = engineOptionsMonitor.CurrentValue;
        this.logger = logger;
    }

    public async Task<bool> ProcessImage(IngestionContext context)
    {
        var (dest, thumb) = CreateRequiredFolders(context.AssetId);

        try
        {
            var flags = new ImageProcessorFlags(context, GetJP2FilePath(context.AssetId, false));
            logger.LogDebug("Got flags '{Flags}' for {AssetId}", flags, context.AssetId);
            var responseModel = await CallImageProcessor(context, flags);
            await ProcessResponse(context, responseModel, flags);
            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error processing image {Asset}", context.Asset.Id);
            context.Asset.Error = $"Appetiser Error: {e.Message}";
            return false;
        }
        finally
        {
            fileSystem.DeleteDirectory(dest, true);
            fileSystem.DeleteDirectory(thumb, true);
        }
    }

    private (string dest, string thumb) CreateRequiredFolders(AssetId assetId)
    {
        var imageIngest = engineSettings.ImageIngest;
        var root = imageIngest.GetRoot();

        // dest is the folder where appetiser will copy output to
        var dest = TemplatedFolders.GenerateFolderTemplate(imageIngest.DestinationTemplate, assetId, root: root);

        // thumb is the folder where generated thumbnails will be output to
        var thumb = TemplatedFolders.GenerateFolderTemplate(imageIngest.ThumbsTemplate, assetId, root: root);

        fileSystem.CreateDirectory(dest);
        fileSystem.CreateDirectory(thumb);
        
        return (dest, thumb);
    }

    // If image is 'use-original' AND a JPEG2000, only generate derivatives (ie thumbs)
    private static bool IsDerivativesOnly(AssetFromOrigin? assetFromOrigin, Asset asset)
        => asset.FullImageOptimisationPolicy.IsUseOriginal() &&
           assetFromOrigin?.ContentType is MIMEHelper.JP2 or MIMEHelper.JPX;

    private async Task<AppetiserResponseModel> CallImageProcessor(IngestionContext context,
        ImageProcessorFlags processorFlags)
    {
        // call tizer/appetiser
        var requestModel = CreateModel(context, processorFlags);

        using var request = new HttpRequestMessage(HttpMethod.Post, "convert");
        request.SetJsonContent(requestModel);

        if (engineSettings.ImageIngest.ImageProcessorDelayMs > 0)
        {
            await Task.Delay(engineSettings.ImageIngest.ImageProcessorDelayMs);
        }

        using var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        // TODO - it's possible to get a 200 when appetiser doesn't do anything, e.g. body not understood
        var responseModel = await response.Content.ReadFromJsonAsync<AppetiserResponseModel>();
        return responseModel;
    }

    private AppetiserRequestModel CreateModel(IngestionContext context, ImageProcessorFlags processorFlags)
    {
        var asset = context.Asset;
        var imageOptimisationPolicy = asset.FullImageOptimisationPolicy;
        if (imageOptimisationPolicy.TechnicalDetails.Length > 1)
        {
            logger.LogWarning(
                "ImageOptimisationPolicy {PolicyId} has {TechDetailsCount} technicalDetails but we can only handle 1",
                imageOptimisationPolicy.Id, imageOptimisationPolicy.TechnicalDetails.Length);
        }

        var requestModel = new AppetiserRequestModel
        {
            Destination = GetJP2FilePath(context.AssetId, true),
            Operation = processorFlags.GenerateDerivativesOnly ? "derivatives-only" : "ingest",
            Optimisation = imageOptimisationPolicy.TechnicalDetails.FirstOrDefault() ?? string.Empty,
            Origin = asset.Origin,
            Source = GetRelativeLocationOnDisk(context),
            ImageId = context.AssetId.Asset,
            JobId = Guid.NewGuid().ToString(),
            ThumbDir = TemplatedFolders.GenerateTemplateForUnix(engineSettings.ImageIngest.ThumbsTemplate,
                context.AssetId, root: engineSettings.ImageIngest.GetRoot(true)),
            // HACK - Appetiser expects to generate thumbs so default to a single 100px thumb
            ThumbSizes = processorFlags.NeedThumbs ? asset.FullThumbnailPolicy.SizeList : new[] { 100 }
        };

        return requestModel;
    }

    private string GetJP2FilePath(AssetId assetId, bool forImageProcessor)
    {
        // Appetiser/Tizer want unix paths relative to mount share.
        // This logic allows handling when running locally on win/unix and when deployed to unix
        var destFolder = forImageProcessor
            ? TemplatedFolders.GenerateTemplateForUnix(engineSettings.ImageIngest.DestinationTemplate,
                assetId, root: engineSettings.ImageIngest.GetRoot(true))
            : TemplatedFolders.GenerateFolderTemplate(engineSettings.ImageIngest.DestinationTemplate,
                assetId, root: engineSettings.ImageIngest.GetRoot());

        return $"{destFolder}{assetId.Asset}.jp2";
    }

    private string GetRelativeLocationOnDisk(IngestionContext context)
    {
        var assetOnDisk = context.AssetFromOrigin.Location;
        var extension = assetOnDisk.EverythingAfterLast('.');

        // this is to get it working nice locally as appetiser/tizer root needs to be unix + relative to it
        var imageProcessorRoot = engineSettings.ImageIngest.GetRoot(true);
        var unixPath = TemplatedFolders.GenerateTemplateForUnix(engineSettings.ImageIngest.SourceTemplate, context.AssetId,
            root: imageProcessorRoot);

        unixPath += $"/{context.Asset.Id.Asset}.{extension}";
        return unixPath;
    }

    private async Task ProcessResponse(IngestionContext context, AppetiserResponseModel responseModel, 
        ImageProcessorFlags processorFlags)
    {
        UpdateImageDimensions(context.Asset, responseModel);

        var imageLocation = await ProcessOriginImage(context, processorFlags);

        await CreateNewThumbs(context, responseModel, processorFlags);

        ImageStorage imageStorage = GetImageStorage(context, processorFlags, responseModel);

        context.WithLocation(imageLocation).WithStorage(imageStorage);
    }

    private static void UpdateImageDimensions(Asset asset, AppetiserResponseModel responseModel)
    {
        asset.Height = responseModel.Height;
        asset.Width = responseModel.Width;
    }

    private async Task<ImageLocation> ProcessOriginImage(IngestionContext context, ImageProcessorFlags processorFlags)
    {
        var asset = context.Asset;
        var imageLocation = new ImageLocation { Id = asset.Id, Nas = string.Empty };
        
        if (!processorFlags.NeedTileOptimised)
        {
            logger.LogDebug("Asset {AssetId} not available on image channel, no further image processing required",
                asset.Id);
            imageLocation.S3 = string.Empty;
            return imageLocation;
        }

        var imageIngestSettings = engineSettings.ImageIngest!;
        
        if (!processorFlags.SaveInDlcsStorage)
        {
            // Optimised - we don't want to store as we've not created a new version - just set imageLocation
            logger.LogDebug("Asset {AssetId} can be served from origin", context.AssetId);
            var originObject = RegionalisedObjectInBucket.Parse(asset.Origin, true)!;
            imageLocation.S3 =
                storageKeyGenerator.GetS3Uri(originObject, imageIngestSettings.IncludeRegionInS3Uri).ToString();
            return imageLocation;
        }
        
        var targetStorageLocation = processorFlags.OriginIsImageServerReady
            ? storageKeyGenerator.GetStoredOriginalLocation(context.AssetId)
            : storageKeyGenerator.GetStorageLocation(context.AssetId);

        // if original is image-server compat., use the 'origin' file to upload. Else upload generated JP2
        var imageServerFile = processorFlags.ImageServerFilePath;

        // Not optimised - upload to DLCS storage and set ImageLocation to new bucket 
        var contentType = processorFlags.OriginIsImageServerReady ? context.Asset.MediaType : MIMEHelper.JP2;

        logger.LogDebug("Asset {Asset} will be stored to {S3Location} with content-type {ContentType}", context.AssetId,
            targetStorageLocation, contentType);
        if (!await bucketWriter.WriteFileToBucket(targetStorageLocation, imageServerFile, contentType))
        {
            throw new ApplicationException(
                $"Failed to write image-server file {imageServerFile} to storage bucket with content-type {contentType}");
        }

        imageLocation.S3 = storageKeyGenerator
            .GetS3Uri(targetStorageLocation, imageIngestSettings.IncludeRegionInS3Uri)
            .ToString();
        return imageLocation;
    }

    private async Task CreateNewThumbs(IngestionContext context, AppetiserResponseModel responseModel,
        ImageProcessorFlags processorFlags)
    {
        if (!processorFlags.NeedThumbs) return;
        
        SetThumbsOnDiskLocation(context, responseModel);

        await thumbCreator.CreateNewThumbs(context.Asset, responseModel.Thumbs.ToList());
    }

    private void SetThumbsOnDiskLocation(IngestionContext context, AppetiserResponseModel responseModel)
    {
        // Update the location of all thumbs to be full path on disk.
        var partialTemplate = TemplatedFolders.GenerateFolderTemplate(engineSettings.ImageIngest.ThumbsTemplate,
            context.AssetId, root: engineSettings.ImageIngest.GetRoot());
        foreach (var thumb in responseModel.Thumbs)
        {
            var key = thumb.Path.EverythingAfterLast('/');
            thumb.Path = string.Concat(partialTemplate, key);
        }
    }

    private ImageStorage GetImageStorage(IngestionContext context, ImageProcessorFlags processorFlags,
        AppetiserResponseModel responseModel)
    {
        var asset = context.Asset;

        // If we are not storing file then size = 0
        var storedImageSize = processorFlags.SaveInDlcsStorage
            ? fileSystem.GetFileSize(processorFlags.ImageServerFilePath)
            : 0L;

        // If thumbs are not required, size = 0
        var thumbSizes = processorFlags.NeedThumbs
            ? responseModel.Thumbs.Sum(t => fileSystem.GetFileSize(t.Path))
            : 0L;

        return new ImageStorage
        {
            Id = asset.Id,
            Customer = asset.Customer,
            Space = asset.Space,
            LastChecked = DateTime.UtcNow,
            Size = storedImageSize,
            ThumbnailSize = thumbSizes
        };
    }

    public class ImageProcessorFlags
    {
        /// <summary>
        /// Flag for whether we have to generate thumbs. For /thumbs/ delivery channel
        /// </summary>
        public bool NeedThumbs { get; }

        /// <summary>
        /// Flag for whether we have to generate tile-optimised JP2. For /iiif-img/ delivery channel
        /// </summary>
        public bool NeedTileOptimised { get; }
        
        /// <summary>
        /// Flag for whether we have to generate derivatives only.
        /// Requires a tile-optimised source image - doesn't mean "generate thumbs only". 
        /// </summary>
        /// <remarks>
        /// This differs from OriginIsImageServerReady because the image must be image-server ready AND also be a
        /// JPEG2000 or appetiser will reject
        /// </remarks>
        public bool GenerateDerivativesOnly { get; }

        /// <summary>
        /// Indicates that either the original, or a derivative, is to be saved in DLCS storage
        /// </summary>
        public bool SaveInDlcsStorage { get; }
        
        /// <summary>
        /// Indicates that the Origin file is suitable for use as image-server source
        /// </summary>
        public bool OriginIsImageServerReady { get; }
        
        /// <summary>
        /// Path on disk where image-server ready file will be located.
        /// This can be the Origin file, or the generated JP2. 
        /// </summary>
        /// <remarks>Used for calculating size and uploading (if required)</remarks>
        public string ImageServerFilePath { get; }

        public override string ToString() =>
            $"derivative-only:{GenerateDerivativesOnly},save:{SaveInDlcsStorage},image-server-ready:{OriginIsImageServerReady}";

        public ImageProcessorFlags(IngestionContext ingestionContext, string jp2OutputPath)
        {
            var assetFromOrigin =
                ingestionContext.AssetFromOrigin.ThrowIfNull(nameof(ingestionContext.AssetFromOrigin))!;

            NeedThumbs = ingestionContext.Asset.HasDeliveryChannel(AssetDeliveryChannels.Thumbs);
            NeedTileOptimised = ingestionContext.Asset.HasDeliveryChannel(AssetDeliveryChannels.Image);

            OriginIsImageServerReady = ingestionContext.Asset.FullImageOptimisationPolicy.IsUseOriginal();
            ImageServerFilePath = OriginIsImageServerReady ? ingestionContext.AssetFromOrigin.Location : jp2OutputPath;
            
            // If image iop 'use-original' and we have a JPEG2000 then only thumbnails are required
            var isJp2 = assetFromOrigin.ContentType is MIMEHelper.JP2 or MIMEHelper.JPX;
            GenerateDerivativesOnly = OriginIsImageServerReady && isJp2;

            // Save in DLCS unless the image is image-server ready AND the strategy is optimised. Will only happen
            // if image delivery-channel available 
            SaveInDlcsStorage = NeedTileOptimised &&
                                !(OriginIsImageServerReady && assetFromOrigin.CustomerOriginStrategy.Optimised);
        }
    }
}