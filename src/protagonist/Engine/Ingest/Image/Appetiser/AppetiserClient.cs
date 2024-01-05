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

            if (responseModel is AppetiserResponseModel successResponse)
            {
                await ProcessResponse(context, successResponse, flags);
                return true;
            }
            else if (responseModel is AppetiserResponseErrorModel failResponse)
            {
                context.Asset.Error = $"Appetiser Error: {failResponse.Message}";
                return false;
            }

            return false;
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

    private async Task<AppetiserResponse> CallImageProcessor(IngestionContext context,
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
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                     
        // TODO - it's possible to get a 200 when appetiser doesn't do anything, e.g. body not understood
        AppetiserResponse? responseModel = null;
        
        if (response.IsSuccessStatusCode)
        {
            responseModel = await response.Content.ReadFromJsonAsync<AppetiserResponseModel>();
        }
        else
        {
            responseModel = await response.Content.ReadFromJsonAsync<AppetiserResponseErrorModel>();
        }

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
        // Update dimensions on Asset
        UpdateImageDimensions(context.Asset, responseModel);

        // Process output: upload derivative/original to DLCS storage if required and set Location + Storage on context 
        await ProcessOriginImage(context, processorFlags);

        // Create new thumbnails + update Storage on context
        await CreateNewThumbs(context, responseModel);
    }

    private static void UpdateImageDimensions(Asset asset, AppetiserResponseModel responseModel)
    {
        asset.Height = responseModel.Height;
        asset.Width = responseModel.Width;
    }

    private async Task ProcessOriginImage(IngestionContext context, ImageProcessorFlags processorFlags)
    {
        var asset = context.Asset;
        var imageIngestSettings = engineSettings.ImageIngest!;

        void SetAssetLocation(ObjectInBucket objectInBucket)
        {
            var s3Location = storageKeyGenerator
                .GetS3Uri(objectInBucket, imageIngestSettings.IncludeRegionInS3Uri)
                .ToString();
            context.WithLocation(new ImageLocation { Id = asset.Id, Nas = string.Empty, S3 = s3Location });
        }

        if (!processorFlags.SaveInDlcsStorage)
        {
            // Optimised + image-server ready. No need to store - set imageLocation to origin and stop
            logger.LogDebug("Asset {AssetId} can be served from origin. No file to save", context.AssetId);
            var originObject = RegionalisedObjectInBucket.Parse(asset.Origin, true)!;
            SetAssetLocation(originObject);
            return;
        }

        RegionalisedObjectInBucket targetStorageLocation;
        if (processorFlags.OriginIsImageServerReady)
        {
            targetStorageLocation = storageKeyGenerator.GetStoredOriginalLocation(context.AssetId);
            if (context.StoredObjects.ContainsKey(targetStorageLocation))
            {
                // Target is image-server ready and should be stored in DLCS but it has already been copied (as part of  
                // "file" delivery-channel handling).
                logger.LogDebug("Asset {AssetId} image-server ready and already uploaded to DLCS storage",
                    context.AssetId);
                SetAssetLocation(targetStorageLocation);
                return;
            }
        }
        else
        {
            // Location for derivative
            targetStorageLocation = storageKeyGenerator.GetStorageLocation(context.AssetId);
            logger.LogDebug("Asset {AssetId} derivative will be stored in DLCS storage", context.AssetId);
        }

        var imageServerFile = processorFlags.ImageServerFilePath;
        var contentType = processorFlags.OriginIsImageServerReady ? context.Asset.MediaType : MIMEHelper.JP2;

        logger.LogDebug("Asset {Asset} will be stored to {S3Location} with content-type {ContentType}", context.AssetId,
            targetStorageLocation, contentType);
        if (!await bucketWriter.WriteFileToBucket(targetStorageLocation, imageServerFile, contentType))
        {
            throw new ApplicationException(
                $"Failed to write image-server file {imageServerFile} to storage bucket with content-type {contentType}");
        }

        var storedImageSize = fileSystem.GetFileSize(processorFlags.ImageServerFilePath);
        context.StoredObjects[targetStorageLocation] = storedImageSize;
        context.WithStorage(assetSize: storedImageSize);

        SetAssetLocation(targetStorageLocation);
    }

    private async Task CreateNewThumbs(IngestionContext context, AppetiserResponseModel responseModel)
    {
        SetThumbsOnDiskLocation(context, responseModel);

        await thumbCreator.CreateNewThumbs(context.Asset, responseModel.Thumbs.ToList());

        var thumbSize = responseModel.Thumbs.Sum(t => fileSystem.GetFileSize(t.Path));
        context.WithStorage(thumbnailSize: thumbSize);
    }

    private void SetThumbsOnDiskLocation(IngestionContext context, AppetiserResponseModel responseModel)
    {
        // Update the location of all thumbs to be full path on disk, relative to orchestrator
        var partialTemplate = TemplatedFolders.GenerateFolderTemplate(engineSettings.ImageIngest.ThumbsTemplate,
            context.AssetId, root: engineSettings.ImageIngest.GetRoot());
        foreach (var thumb in responseModel.Thumbs)
        {
            var key = thumb.Path.EverythingAfterLast('/');
            thumb.Path = string.Concat(partialTemplate, key);
        }
    }

    public class ImageProcessorFlags
    {
        /// <summary>
        /// Flag for whether we have to generate derivatives (ie thumbs) only.
        /// Requires a tile-optimised source image.
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

            OriginIsImageServerReady = ingestionContext.Asset.FullImageOptimisationPolicy.IsUseOriginal();
            ImageServerFilePath = OriginIsImageServerReady ? ingestionContext.AssetFromOrigin.Location : jp2OutputPath;
            
            // If image iop 'use-original' and we have a JPEG2000 then only thumbnails are required
            var isJp2 = assetFromOrigin.ContentType is MIMEHelper.JP2 or MIMEHelper.JPX;
            GenerateDerivativesOnly = OriginIsImageServerReady && isJp2;

            // Save in DLCS unless the image is image-server ready AND the strategy is optimised 
            SaveInDlcsStorage = !(OriginIsImageServerReady && assetFromOrigin.CustomerOriginStrategy.Optimised);
        }
    }
}