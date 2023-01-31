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
        EnsureRequiredFoldersExist(context.AssetId);

        try
        {
            var flags = new ImageProcessorFlags(context);
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
    }

    private void EnsureRequiredFoldersExist(AssetId assetId)
    {
        var imageIngest = engineSettings.ImageIngest;
        var root = imageIngest.GetRoot();

        // dest is the folder where appetiser will copy output to
        var dest = TemplatedFolders.GenerateFolderTemplate(imageIngest.DestinationTemplate, assetId, root: root);

        // thumb is the folder where generated thumbnails will be output to
        var thumb = TemplatedFolders.GenerateFolderTemplate(imageIngest.ThumbsTemplate, assetId, root: root);

        fileSystem.CreateDirectory(dest);
        fileSystem.CreateDirectory(thumb);
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
            ThumbSizes = asset.FullThumbnailPolicy.SizeList
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
        UpdateImageSize(context.Asset, responseModel);

        var imageLocation = await ProcessOriginImage(context, processorFlags);

        await CreateNewThumbs(context, responseModel);

        ImageStorage imageStorage = GetImageStorage(context, responseModel);

        context.WithLocation(imageLocation).WithStorage(imageStorage);
    }

    private static void UpdateImageSize(Asset asset, AppetiserResponseModel responseModel)
    {
        asset.Height = responseModel.Height;
        asset.Width = responseModel.Width;
    }

    private async Task<ImageLocation> ProcessOriginImage(IngestionContext context, ImageProcessorFlags processorFlags)
    {
        var asset = context.Asset;
        var imageLocation = new ImageLocation { Id = asset.Id, Nas = string.Empty };

        var imageIngestSettings = engineSettings.ImageIngest;
        
        if (!processorFlags.SaveInDlcsStorage)
        {
            // Optimised - we don't want to store as we've not created a new version - just set imageLocation
            logger.LogDebug("Asset {AssetId} can be served from origin", context.AssetId);
            var originObject = RegionalisedObjectInBucket.Parse(asset.Origin);
            imageLocation.S3 =
                storageKeyGenerator.GetS3Uri(originObject, imageIngestSettings.IncludeRegionInS3Uri).ToString();
            return imageLocation;
        }
        
        var jp2BucketObject = storageKeyGenerator.GetStorageLocation(context.AssetId);

        // if original is image-server compat., use the 'origin' file to upload. Else upload generated JP2
        var jp2File = processorFlags.OriginIsImageServerReady
            ? context.AssetFromOrigin.Location
            : GetJP2FilePath(context.AssetId, false);

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

    private async Task CreateNewThumbs(IngestionContext context, AppetiserResponseModel responseModel)
    {
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
    
    private ImageStorage GetImageStorage(IngestionContext context, AppetiserResponseModel responseModel)
    {
        var asset = context.Asset;

        var jp2Size = fileSystem.GetFileSize(GetJP2FilePath(context.AssetId, false));
        var thumbSizes = responseModel.Thumbs.Sum(t => fileSystem.GetFileSize(t.Path));

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
    
    public class ImageProcessorFlags
    {
        /// <summary>
        /// Should we generate thumbnails only?
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

        public ImageProcessorFlags(IngestionContext ingestionContext)
        {
            var assetFromOrigin =
                ingestionContext.AssetFromOrigin.ThrowIfNull(nameof(ingestionContext.AssetFromOrigin))!;
            
            OriginIsImageServerReady = ingestionContext.Asset.FullImageOptimisationPolicy.IsUseOriginal();
            var isJp2 = assetFromOrigin.ContentType is MIMEHelper.JP2 or MIMEHelper.JPX;
            
            // If image iop 'use-original' and we have a JPEG2000 then only thumbnails are required
            GenerateDerivativesOnly = OriginIsImageServerReady && isJp2;

            // Save in DLCS unless the image is image-server ready AND the strategy is optimised
            SaveInDlcsStorage = !(OriginIsImageServerReady && assetFromOrigin.CustomerOriginStrategy.Optimised);
        }
    }
}