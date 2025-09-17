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
using Engine.Ingest.Image.ImageServer.Clients;
using Engine.Ingest.Image.ImageServer.Models;
using Engine.Ingest.Persistence;
using Engine.Settings;
using Microsoft.Extensions.Options;

namespace Engine.Ingest.Image.ImageServer;

/// <summary>
/// Derivative generator using Appetiser for generating JP2 and Thumbs
/// </summary>
public class AppetiserImageProcessor : IImageProcessor
{
    private readonly IImageProcessorClient appetiserClient;
    private readonly IThumbsClient thumbsClient;
    private readonly EngineSettings engineSettings;
    private readonly ILogger<AppetiserImageProcessor> logger;
    private readonly IBucketWriter bucketWriter;
    private readonly IStorageKeyGenerator storageKeyGenerator;
    private readonly IThumbCreator thumbCreator;
    private readonly IFileSystem fileSystem;

    public AppetiserImageProcessor(
        IImageProcessorClient appetiserClient,
        IThumbsClient thumbsClient,
        IBucketWriter bucketWriter,
        IStorageKeyGenerator storageKeyGenerator,
        IThumbCreator thumbCreator,
        IFileSystem fileSystem,
        IOptionsMonitor<EngineSettings> engineOptionsMonitor,
        ILogger<AppetiserImageProcessor> logger)
    {
        this.appetiserClient = appetiserClient;
        this.thumbsClient = thumbsClient;
        this.bucketWriter = bucketWriter;
        this.storageKeyGenerator = storageKeyGenerator;
        this.thumbCreator = thumbCreator;
        this.fileSystem = fileSystem;
        engineSettings = engineOptionsMonitor.CurrentValue;
        this.logger = logger;
    }
    
    public async Task<bool> ProcessImage(IngestionContext context)
    {
        var modifiedAssetId = new AssetId(context.AssetId.Customer, context.AssetId.Space,
            context.AssetId.GetDiskSafeAssetId(engineSettings.ImageIngest));
        var (dest, thumb) = CreateRequiredFolders(modifiedAssetId, context.IngestId);

        try
        {
            var flags = new ImageProcessorFlags(context, appetiserClient.GetJP2FilePath(modifiedAssetId, context.IngestId, false));
            logger.LogDebug("Got flags '{@Flags}' for {AssetId}", flags, context.AssetId);
            var responseModel = await appetiserClient.GenerateJP2(context, modifiedAssetId);

            if (responseModel is AppetiserResponseModel successResponse)
            {
                await ProcessResponse(context, successResponse, flags);
                await CallThumbsProcessor(context, thumb);
                
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
            context.Asset.Error = $"Image Server Error: {e.Message}";
            return false;
        }
        finally
        {
            fileSystem.DeleteDirectory(dest, true);
            fileSystem.DeleteDirectory(thumb, true);
        }
    }

    private (string dest, string thumb) CreateRequiredFolders(AssetId assetId, string ingestId)
    {
        var imageIngest = engineSettings.ImageIngest;
        var workingFolder = ImageIngestionHelpers.GetWorkingFolder(ingestId, imageIngest);

        // dest is the folder where appetiser will copy output to
        var dest = TemplatedFolders.GenerateFolderTemplate(imageIngest.DestinationTemplate, assetId, root: workingFolder);

        // thumb is the folder where generated thumbnails will be output to
        var thumb = TemplatedFolders.GenerateFolderTemplate(imageIngest.ThumbsTemplate, assetId, root: workingFolder);

        fileSystem.CreateDirectory(dest);
        fileSystem.CreateDirectory(thumb);
        
        return (dest, thumb);
    }
    
    private async Task CallThumbsProcessor(IngestionContext context, string thumbFolder)
    {
        var thumbPolicy = context.Asset.ImageDeliveryChannels
            .GetThumbsChannel()?.DeliveryChannelPolicy
            .PolicyDataAs<List<string>>();
        
        var sizes = engineSettings.ImageIngest!.DefaultThumbs;

        if (thumbPolicy != null)
        {
            sizes = sizes.Union(thumbPolicy).ToList();
        }

        var thumbsResponse = await thumbsClient.GenerateThumbnails(context, sizes, thumbFolder);

        // Create new thumbnails + update Storage on context
        await CreateNewThumbs(context, thumbsResponse);
    }

    private async Task ProcessResponse(IngestionContext context, AppetiserResponseModel responseModel, 
        ImageProcessorFlags processorFlags)
    {
        // Update dimensions on Asset
        UpdateImageDimensions(context.Asset, responseModel);

        // Process output: upload derivative/original to DLCS storage if required and set Location + Storage on context 
        await ProcessOriginImage(context, processorFlags);
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

        if (processorFlags.AlreadyUploaded)
        {
            // file has been uploaded already, and no image is required
            logger.LogDebug("Asset {AssetId} has been uploaded already from the file channel, and no image delivery channel has been specified", context.AssetId);
            SetAssetLocation(context.StoredObjects.Keys.First());
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
        else if (processorFlags.IsTransient)
        {
            // transient asset, means it can be cleaned up after a set period of time
            logger.LogDebug("Asset {AssetId} is transient. S3 marker will be added", context.AssetId);
            targetStorageLocation = storageKeyGenerator.GetTransientImageLocation(context.AssetId);
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

        if (!processorFlags.IsTransient) // transient images get deleted, so no need to store asset sizes
        {
            var storedImageSize = fileSystem.GetFileSize(processorFlags.ImageServerFilePath);
            context.StoredObjects[targetStorageLocation] = storedImageSize;
            context.WithStorage(assetSize: storedImageSize);
        }
        
        SetAssetLocation(targetStorageLocation);
    }

    private async Task CreateNewThumbs(IngestionContext context, List<ImageOnDisk> thumbs)
    {
        await thumbCreator.CreateNewThumbs(context.Asset, thumbs.ToList());

        var thumbSize = thumbs.Sum(t => fileSystem.GetFileSize(t.Path));
        context.WithStorage(thumbnailSize: thumbSize);
    }
    
    public class ImageProcessorFlags
    {
        private readonly List<string> derivativesOnlyPolicies = ["use-original"];
        
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
        /// Whether the file for thumbnail generation is stored at a transient location
        /// </summary>
        /// <remarks>
        /// This differs from OriginIsImageServerReady because the image explicitly only has a thumbs channel set.
        /// </remarks>
        [Obsolete("Was required for EngineThumbs")]
        public bool IsTransient { get; }

        /// <summary>
        /// Indicates that either the original, or a derivative, is to be saved in DLCS storage
        /// </summary>
        public bool SaveInDlcsStorage { get; }
        
        /// <summary>
        /// Indicates that the Origin file is suitable for use as image-server source
        /// </summary>
        public bool OriginIsImageServerReady { get; }
        
        /// <summary>
        /// Indicates that the origin has already been uploaded to S3
        /// </summary>
        [Obsolete("Was required for EngineThumbs")]
        public bool AlreadyUploaded { get; set; }
        
        /// <summary>
        /// Path on disk where image-server ready file will be located.
        /// This can be the Origin file, or the generated JP2. 
        /// </summary>
        /// <remarks>Used for calculating size and uploading (if required)</remarks>
        public string ImageServerFilePath { get; }

        public ImageProcessorFlags(IngestionContext ingestionContext, string jp2OutputPath)
        {
            var assetFromOrigin =
                ingestionContext.AssetFromOrigin.ThrowIfNull(nameof(ingestionContext.AssetFromOrigin));

            var hasImageDeliveryChannel = ingestionContext.Asset.HasDeliveryChannel(AssetDeliveryChannels.Image);

            var imagePolicy = hasImageDeliveryChannel
                ? ingestionContext.Asset.ImageDeliveryChannels.GetImageChannel()?.DeliveryChannelPolicy.Name
                : null;
            
            // only set image server ready if an image server ready policy is set explicitly
            OriginIsImageServerReady = imagePolicy != null && derivativesOnlyPolicies.Contains(imagePolicy);
            ImageServerFilePath = OriginIsImageServerReady ? assetFromOrigin.Location : jp2OutputPath;
            
            // If image iop 'use-original' and we have a JPEG2000 then only thumbnails are required
            var isJp2 = assetFromOrigin.ContentType is MIMEHelper.JP2 or MIMEHelper.JPX;
            GenerateDerivativesOnly = OriginIsImageServerReady && isJp2;

            IsTransient = !hasImageDeliveryChannel;

            AlreadyUploaded = ingestionContext.Asset.HasDeliveryChannel(AssetDeliveryChannels.File) && 
                              !hasImageDeliveryChannel;

            // Save in DLCS unless the image is image-server ready AND the strategy is optimised 
            SaveInDlcsStorage = !((OriginIsImageServerReady || IsTransient) && assetFromOrigin.CustomerOriginStrategy.Optimised);
        }
    }
}
