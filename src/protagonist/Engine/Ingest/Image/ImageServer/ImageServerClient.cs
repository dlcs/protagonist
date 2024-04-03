using System.Text.Json;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.Core;
using DLCS.Core.FileSystem;
using DLCS.Core.Guard;
using DLCS.Core.Strings;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Templates;
using Engine.Ingest.Image.ImageServer.Clients;
using Engine.Ingest.Image.ImageServer.Models;
using Engine.Settings;
using Microsoft.Extensions.Options;

namespace Engine.Ingest.Image.ImageServer;

/// <summary>
/// Derivative generator using Appetiser for generating resources
/// </summary>
public class ImageServerClient : IImageProcessor
{
    private readonly IAppetiserClient appetiserClient;
    private readonly ICantaloupeThumbsClient thumbsClient;
    private readonly EngineSettings engineSettings;
    private readonly ILogger<ImageServerClient> logger;
    private readonly IBucketWriter bucketWriter;
    private readonly IStorageKeyGenerator storageKeyGenerator;
    private readonly IThumbCreator thumbCreator;
    private readonly IFileSystem fileSystem;

    public ImageServerClient(
        IAppetiserClient appetiserClient,
        ICantaloupeThumbsClient thumbsClient,
        IBucketWriter bucketWriter,
        IStorageKeyGenerator storageKeyGenerator,
        IThumbCreator thumbCreator,
        IFileSystem fileSystem,
        IOptionsMonitor<EngineSettings> engineOptionsMonitor,
        ILogger<ImageServerClient> logger)
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
            context.AssetId.Asset.Replace("(", engineSettings.ImageIngest.OpenBracketReplacement)
                .Replace(")", engineSettings.ImageIngest.CloseBracketReplacement));
        var (dest, thumb) = CreateRequiredFolders(modifiedAssetId);

        try
        {
            var flags = new ImageProcessorFlags(context, GetJP2FilePath(modifiedAssetId, false));
            logger.LogDebug("Got flags '{@Flags}' for {AssetId}", flags, context.AssetId);
            var responseModel = await CallImageProcessor(context, flags, modifiedAssetId);

            if (responseModel is AppetiserResponseModel successResponse)
            {
                await ProcessResponse(context, successResponse, flags, modifiedAssetId);
                await CallThumbsProcessor(context, modifiedAssetId);
                
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
    
    private async Task<IAppetiserResponse> CallImageProcessor(IngestionContext context,
        ImageProcessorFlags processorFlags, AssetId modifiedAssetId)
    {
        // call tizer/appetiser
        var requestModel = CreateModel(context, modifiedAssetId, processorFlags);
        IAppetiserResponse? responseModel;

        responseModel = await appetiserClient.CallAppetiser(requestModel);

        return responseModel;
    }
    
    private async Task CallThumbsProcessor(IngestionContext context,
        AssetId modifiedAssetId)
    {
        var thumbPolicy = context.Asset.ImageDeliveryChannels.SingleOrDefault(
                x=> x.Channel == AssetDeliveryChannels.Thumbnails)
            ?.DeliveryChannelPolicy.PolicyData;

        var thumbsResponse = new List<ImageOnDisk>();

        if (thumbPolicy != null)
        {
            var sizes = JsonSerializer.Deserialize<List<string>>(thumbPolicy);
            thumbsResponse = await thumbsClient.CallCantaloupe(context, modifiedAssetId, sizes);
        }
        
        // Create new thumbnails + update Storage on context
        await CreateNewThumbs(context, thumbsResponse);
    }

    private AppetiserRequestModel CreateModel(IngestionContext context, AssetId modifiedAssetId, ImageProcessorFlags processorFlags)
    {
        var requestModel = new AppetiserRequestModel
        {
            Destination = GetJP2FilePath(modifiedAssetId, true),
            Operation = "image-only",
            Optimisation = "kdu_max",
            Origin = context.Asset.Origin,
            Source = GetRelativeLocationOnDisk(context, modifiedAssetId),
            ImageId = context.AssetId.Asset,
            JobId = Guid.NewGuid().ToString(),
            ThumbDir = TemplatedFolders.GenerateTemplateForUnix(engineSettings.ImageIngest.ThumbsTemplate,
                modifiedAssetId, root: engineSettings.ImageIngest.GetRoot(true)),
            ThumbSizes = new int[1]
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

    private string GetRelativeLocationOnDisk(IngestionContext context, AssetId modifiedAssetId)
    {
        var assetOnDisk = context.AssetFromOrigin.Location;
        var extension = assetOnDisk.EverythingAfterLast('.');

        // this is to get it working nice locally as appetiser/tizer root needs to be unix + relative to it
        var imageProcessorRoot = engineSettings.ImageIngest.GetRoot(true);
        var unixPath = TemplatedFolders.GenerateTemplateForUnix(engineSettings.ImageIngest.SourceTemplate, modifiedAssetId,
            root: imageProcessorRoot);

        unixPath += $"/{modifiedAssetId.Asset}.{extension}";
        return unixPath;
    }

    private async Task ProcessResponse(IngestionContext context, AppetiserResponseModel responseModel, 
        ImageProcessorFlags processorFlags, AssetId modifiedAssetId)
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
        private readonly List<string> derivativesOnlyPolicies = new List<string>()
        {
            "use-original"
        };

        /// <summary>
        /// Whether the origin is transient
        /// </summary>
        /// <remarks>
        /// This differs from OriginIsImageServerReady because the image explicitly only has a thumbs channel set.
        /// </remarks>
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
                ingestionContext.AssetFromOrigin.ThrowIfNull(nameof(ingestionContext.AssetFromOrigin))!;

            var hasImageDeliveryChannel = ingestionContext.Asset.HasDeliveryChannel(AssetDeliveryChannels.Image);
            
            var imagePolicy = hasImageDeliveryChannel ? ingestionContext.Asset.ImageDeliveryChannels.SingleOrDefault(
                    x=> x.Channel == AssetDeliveryChannels.Image)
                ?.DeliveryChannelPolicy.Name : null;

            OriginIsImageServerReady = imagePolicy != null || derivativesOnlyPolicies.Contains(imagePolicy); // only set image server ready if an image server ready policy is set explicitly
            ImageServerFilePath = OriginIsImageServerReady ? ingestionContext.AssetFromOrigin.Location : jp2OutputPath;

            IsTransient = !hasImageDeliveryChannel;

            AlreadyUploaded = ingestionContext.Asset.HasDeliveryChannel(AssetDeliveryChannels.File) && 
                              !hasImageDeliveryChannel;

            // Save in DLCS unless the image is image-server ready AND the strategy is optimised 
            SaveInDlcsStorage = !((OriginIsImageServerReady || IsTransient) && assetFromOrigin.CustomerOriginStrategy.Optimised);
        }
    }
}