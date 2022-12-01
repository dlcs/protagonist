using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.Core;
using DLCS.Core.FileSystem;
using DLCS.Core.Strings;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Customers;
using DLCS.Model.Templates;
using DLCS.Web.Requests;
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
        /*
         * The outcome can be that we generate:
         *   thumbs only, because ["thumbs"] OR ["thumbs", "iiif-img"] but it's already optimised
         *   image only, because ["iiif-img"]
         *   both, because ["thumbs", "iiif-img"]
         *   nothing, because ["iiif-img"] and already optimised
         */
        var (dest, thumb) = GetFolders(context.AssetId);
        
        fileSystem.CreateDirectory(dest);
        fileSystem.CreateDirectory(thumb);

        try
        {
            var flags = ProcessFlags.Create(context);
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
    
    private (string dest, string thumb) GetFolders(AssetId assetId)
    {
        var imageIngest = engineSettings.ImageIngest;
        var root = imageIngest.GetRoot();

        // dest is the folder where appetiser will copy output to
        var dest = TemplatedFolders.GenerateFolderTemplate(imageIngest.DestinationTemplate, assetId, root: root);

        // thumb is the folder where generated thumbnails will be output to
        var thumb = TemplatedFolders.GenerateFolderTemplate(imageIngest.ThumbsTemplate, assetId, root: root);
        return (dest, thumb);
    }
    
    private async Task<AppetiserResponseModel> CallImageProcessor(IngestionContext context, ProcessFlags flags)
    {
        if (!flags.NeedThumbs && flags.OriginTileOptimised)
        {
            logger.LogDebug("Asset {AssetId} does not need thumbs and is tile-optimised so no processing to do",
                context.AssetId);
            return new AppetiserResponseModel();
        }
        
        // call tizer/appetiser
        logger.LogDebug("Calling Appetiser for {AssetId}..", context.AssetId);
        var requestModel = CreateModel(context, flags);

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

    private AppetiserRequestModel CreateModel(IngestionContext context, ProcessFlags flags)
    {
        var asset = context.Asset;
        var imageOptimisationPolicy = asset.FullImageOptimisationPolicy;
        if (imageOptimisationPolicy.TechnicalDetails.Length > 1)
        {
            logger.LogWarning(
                "ImageOptimisationPolicy {PolicyId} has {TechDetailsCount} technicalDetails but Appetiser can only handle 1",
                imageOptimisationPolicy.Id, imageOptimisationPolicy.TechnicalDetails.Length);
        }

        var requestModel = new AppetiserRequestModel
        {
            Destination = GetJP2File(context.AssetId, true),
            Operation = flags.DerivativesOnly ? "derivatives-only" : "ingest",
            Optimisation = imageOptimisationPolicy.TechnicalDetails.FirstOrDefault() ?? string.Empty,
            Origin = asset.Origin,
            Source = GetRelativeLocationOnDisk(context),
            ImageId = context.AssetId.Asset,
            JobId = Guid.NewGuid().ToString(),
            ThumbDir = TemplatedFolders.GenerateTemplateForUnix(engineSettings.ImageIngest.ThumbsTemplate,
                context.AssetId, root: engineSettings.ImageIngest.GetRoot(true)),
            // HACK - Appetiser expects to generate thumbs so default to a single 100px thumb
            ThumbSizes = flags.NeedThumbs ? asset.FullThumbnailPolicy.SizeList : new []{ 100 }  
        };

        return requestModel;
    }

    private string GetJP2File(AssetId assetId, bool forImageProcessor)
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
        ProcessFlags flags)
    {
        UpdateImageSize(context.Asset, responseModel, flags);

        var imageLocation = await ProcessTileOptimisedImage(context, flags);

        await CreateNewThumbs(context, responseModel, flags);

        ImageStorage imageStorage = GetImageStorage(context, responseModel, flags);

        context.WithLocation(imageLocation).WithStorage(imageStorage);
    }

    private static void UpdateImageSize(Asset asset, AppetiserResponseModel responseModel, ProcessFlags flags)
    {
        asset.Height = responseModel.Height;
        asset.Width = responseModel.Width;
    }

    private async Task<ImageLocation> ProcessTileOptimisedImage(IngestionContext context, ProcessFlags flags)
    {
        var asset = context.Asset;
        var imageLocation = new ImageLocation { Id = asset.Id, Nas = string.Empty};

        if (!flags.NeedTileOptimised)
        {
            logger.LogDebug("Asset {AssetId} not available on image channel, no further image processing required",
                asset.Id);
            imageLocation.S3 = string.Empty;
            return imageLocation;
        }

        var imageIngestSettings = engineSettings.ImageIngest;

        if (flags.OriginTileOptimised)
        {
            // Optimised strategy - we don't want to store in S3 as we've not created a new version - set imageLocation
            logger.LogDebug("Asset {AssetId} origin is optimised s3Ambient strategy. No need to store",
                context.AssetId);
            var originObject = RegionalisedObjectInBucket.Parse(asset.Origin);
            imageLocation.S3 =
                storageKeyGenerator.GetS3Uri(originObject, imageIngestSettings.IncludeRegionInS3Uri).ToString();
            return imageLocation;
        }

        var originStrategy = context.AssetFromOrigin.CustomerOriginStrategy;
        if (originStrategy.Optimised)
        {
            logger.LogWarning(
                "Asset {AssetId} has originStrategy '{OriginStrategy}' ({OriginStrategyId}), which is optimised but not S3",
                context.AssetId, originStrategy.Strategy, originStrategy.Id);
        }

        var jp2BucketObject = storageKeyGenerator.GetStorageLocation(context.AssetId);

        // if derivatives-only, no new JP2 will have been generated so use the 'origin' file
        var jp2File = flags.DerivativesOnly ? context.AssetFromOrigin.Location : GetJP2File(context.AssetId, false);

        // Not optimised - upload JP2 to S3 and set ImageLocation to new bucket location
        if (!await bucketWriter.WriteFileToBucket(jp2BucketObject, jp2File, MIMEHelper.JP2))
        {
            throw new ApplicationException($"Failed to write jp2 {jp2File} to storage bucket");
        }

        imageLocation.S3 = storageKeyGenerator
            .GetS3Uri(jp2BucketObject, imageIngestSettings.IncludeRegionInS3Uri)
            .ToString();
        return imageLocation;
    }

    private async Task CreateNewThumbs(IngestionContext context, AppetiserResponseModel responseModel,
        ProcessFlags flags)
    {
        if (!flags.NeedThumbs) return;

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

    private ImageStorage GetImageStorage(IngestionContext context, AppetiserResponseModel responseModel,
        ProcessFlags flags)
    {
        var asset = context.Asset;

        // Only count jp2 size if we will be serving AND we're storing it (which we aren't if originTileOptimised)
        var jp2Size = flags.NeedTileOptimised && !flags.OriginTileOptimised
            ? fileSystem.GetFileSize(GetJP2File(context.AssetId, false))
            : 0;
        
        // Only count thumbs if we're serving 
        var thumbSizes = flags.NeedThumbs ? responseModel.Thumbs.Sum(t => fileSystem.GetFileSize(t.Path)) : 0;

        return new ImageStorage
        {
            Id = asset.Id,
            Customer = asset.Customer,
            Space = asset.Space,
            LastChecked = DateTime.UtcNow,
            Size = jp2Size,
            ThumbnailSize = thumbSizes
        };
    }
    
    private class ProcessFlags
    {
        /// <summary>
        /// Flag for whether we have to generate thumbs. For /thumbs/ delivery channel
        /// </summary>
        public bool NeedThumbs { get; private init; }

        /// <summary>
        /// Flag for whether we have to generate tile-optimised JP2. For /iiif-img/ delivery channel
        /// </summary>
        public bool NeedTileOptimised { get; private init; }

        /// <summary>
        /// Flag for whether we have to generate derivatives only.
        /// Requires a tile-optimised source image - doesn't mean "generate thumbs only". 
        /// </summary>
        public bool DerivativesOnly { get; private init; }
        
        /// <summary>
        /// Flag that indicates whether the origin image is already tile-optimised and we can use the origin for serving
        /// image requests
        /// </summary>
        /// <remarks>This logic will change in the future with enhancements to customerOriginStrategy</remarks>
        public bool OriginTileOptimised { get; private init; }

        public static ProcessFlags Create(IngestionContext context)
        {
            // Set flags required for processing request
            var thumbs = context.Asset.HasDeliveryChannel(AssetDeliveryChannels.Thumbs);
            var tileOptimised = context.Asset.HasDeliveryChannel(AssetDeliveryChannels.Image);
            
            var originStrategy = context.AssetFromOrigin!.CustomerOriginStrategy;
            var originTileOptimised = originStrategy.Optimised &&
                                      originStrategy.Strategy == OriginStrategyType.S3Ambient &&
                                      context.AssetFromOrigin.ContentType is MIMEHelper.JP2 or MIMEHelper.JPX;

            return new ProcessFlags
            {
                NeedThumbs = thumbs,
                NeedTileOptimised = tileOptimised,
                DerivativesOnly = tileOptimised && originTileOptimised,
                OriginTileOptimised = originTileOptimised,
            };
        }
    }
}