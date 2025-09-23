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
using Engine.Ingest.Image.ImageServer.Clients;
using Engine.Ingest.Image.ImageServer.Models;
using Engine.Ingest.Persistence;
using Engine.Settings;
using IIIF.ImageApi;
using Microsoft.Extensions.Options;

namespace Engine.Ingest.Image.ImageServer;

/// <summary>
/// Derivative generator using Appetiser for generating JP2 and Thumbs
/// </summary>
public class AppetiserImageProcessor(
    IImageProcessorClient appetiserClient,
    IBucketWriter bucketWriter,
    IStorageKeyGenerator storageKeyGenerator,
    IThumbCreator thumbCreator,
    IFileSystem fileSystem,
    IOptionsMonitor<EngineSettings> engineOptionsMonitor,
    ILogger<AppetiserImageProcessor> logger)
    : IImageProcessor
{
    private readonly ImageIngestSettings imageIngestSettings = engineOptionsMonitor.CurrentValue.ImageIngest!;

    public async Task<bool> ProcessImage(IngestionContext context)
    {
        var modifiedAssetId = new AssetId(context.AssetId.Customer, context.AssetId.Space,
            context.AssetId.GetDiskSafeAssetId(imageIngestSettings));
        var (dest, thumb) = CreateRequiredFolders(modifiedAssetId, context.IngestId);

        try
        {
            var flags = new ImageProcessorFlags(context, appetiserClient.GetJP2FilePath(modifiedAssetId, context.IngestId, false));
            logger.LogDebug("Got flags '{@Flags}' for {AssetId}", flags, context.AssetId);
            var sizes = GetThumbSizes(context);
            var responseModel =
                await appetiserClient.GenerateDerivatives(context, modifiedAssetId, sizes, flags.Operations);

            if (responseModel is AppetiserResponseModel successResponse)
            {
                await ProcessResponse(context, successResponse, flags, modifiedAssetId);
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
        var workingFolder = ImageIngestionHelpers.GetWorkingFolder(ingestId, imageIngestSettings);

        // dest is the folder where appetiser will copy output to
        var dest = TemplatedFolders.GenerateFolderTemplate(imageIngestSettings.DestinationTemplate, assetId,
            root: workingFolder);

        // thumb is the folder where generated thumbnails will be output to
        var thumb = TemplatedFolders.GenerateFolderTemplate(imageIngestSettings.ThumbsTemplate, assetId,
            root: workingFolder);

        fileSystem.CreateDirectory(dest);
        fileSystem.CreateDirectory(thumb);
        
        return (dest, thumb);
    }
    
    private List<SizeParameter> GetThumbSizes(IngestionContext context)
    {
        var thumbPolicy = context.Asset.ImageDeliveryChannels
            .GetThumbsChannel()?.DeliveryChannelPolicy
            .PolicyDataAs<List<string>>();
        
        var sizes = imageIngestSettings.DefaultThumbs;

        if (thumbPolicy != null)
        {
            sizes = sizes.Union(thumbPolicy).ToList();
        }
        
        return sizes.Select(SizeParameter.Parse).ToList();
    }

    private async Task ProcessResponse(IngestionContext context, AppetiserResponseModel responseModel, 
        ImageProcessorFlags processorFlags, AssetId modifiedAssetId)
    {
        // Update dimensions on Asset
        UpdateImageDimensions(context.Asset, responseModel);

        // Process output: upload derivative/original to DLCS storage if required and set Location + Storage on context 
        await ProcessOriginImage(context, processorFlags);
        
        // Create new thumbnails + update Storage on context
        await CreateNewThumbs(context, responseModel, modifiedAssetId);
    }

    private static void UpdateImageDimensions(Asset asset, AppetiserResponseModel responseModel)
    {
        asset.Height = responseModel.Height;
        asset.Width = responseModel.Width;
    }

    private async Task ProcessOriginImage(IngestionContext context, ImageProcessorFlags processorFlags)
    {
        var asset = context.Asset;

        if (!processorFlags.SaveInDlcsStorage)
        {
            // Optimised + either image-server ready OR no image channel. No need to store - set imageLocation to origin
            logger.LogDebug("Asset {AssetId} can be served from origin. No file to save", context.AssetId);
            var originObject = RegionalisedObjectInBucket.Parse(asset.Origin!, true)!;
            SetAssetLocation(originObject);
            return;
        }

        if (processorFlags.AlreadyUploadedNoImage)
        {
            // Origin has been uploaded already, and no image-channel so no need to upload derivative
            logger.LogDebug("Asset {AssetId} uploaded for file channel and no image delivery channel", context.AssetId);
            SetAssetLocation(storageKeyGenerator.GetStoredOriginalLocation(context.AssetId));
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

        void SetAssetLocation(ObjectInBucket objectInBucket)
        {
            var s3Location = storageKeyGenerator
                .GetS3Uri(objectInBucket, imageIngestSettings.IncludeRegionInS3Uri)
                .ToString();
            context.WithLocation(new ImageLocation { Id = asset.Id, Nas = string.Empty, S3 = s3Location });
        }
    }

    private async Task CreateNewThumbs(IngestionContext context, AppetiserResponseModel responseModel,
        AssetId modifiedAssetId)
    {
        SetThumbsOnDiskLocation(responseModel, modifiedAssetId);

        await thumbCreator.CreateNewThumbs(context.Asset, responseModel.Thumbs.ToList());

        var thumbSize = responseModel.Thumbs.Sum(t => fileSystem.GetFileSize(t.Path));
        context.WithStorage(thumbnailSize: thumbSize);
    }
    
    private void SetThumbsOnDiskLocation(AppetiserResponseModel responseModel, AssetId modifiedAssetId)
    {
        // Update the location of all thumbs to be full path on disk, relative to orchestrator
        var partialTemplate = TemplatedFolders.GenerateFolderTemplate(imageIngestSettings.ThumbsTemplate,
            modifiedAssetId, root: imageIngestSettings.GetRoot());
        foreach (var thumb in responseModel.Thumbs)
        {
            var key = thumb.Path.EverythingAfterLast('/');
            thumb.Path = string.Concat(partialTemplate, key);
        }
    }

    public class ImageProcessorFlags
    {
        private readonly List<string> derivativesOnlyPolicies = ["use-original"];

        /// <summary>
        /// Flags for what operations are required when processing image
        /// </summary>
        /// <remarks>
        /// This differs from OriginIsImageServerReady because the image must be image-server ready AND also be a
        /// JPEG2000 or appetiser will reject
        /// </remarks>
        public ImageProcessorOperations Operations { get; }

        /// <summary>
        /// Indicates that either the original, or a derivative, is to be saved in DLCS storage
        /// </summary>
        public bool SaveInDlcsStorage { get; }

        /// <summary>
        /// Indicates that the Origin file is suitable for use as image-server source
        /// </summary>
        public bool OriginIsImageServerReady { get; }

        /// <summary>
        /// Indicates that the origin has already been uploaded to S3 and there is no image delivery channel
        /// </summary>
        public bool AlreadyUploadedNoImage { get; set; }

        /// <summary>
        /// Path on disk where image-server ready file will be located.
        /// This can be the Origin file as-is, or the generated JP2. 
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

            // We will always be generating thumbs
            Operations = ImageProcessorOperations.Thumbnails;

            // We will want to generate a JP2 derivative unless it's use-original
            if (hasImageDeliveryChannel && !OriginIsImageServerReady)
            {
                Operations |= ImageProcessorOperations.Derivative;
            }

            AlreadyUploadedNoImage = ingestionContext.Asset.HasDeliveryChannel(AssetDeliveryChannels.File) &&
                                     !hasImageDeliveryChannel;

            // Save in DLCS unless the image is image-server ready AND the strategy is optimised
            if (!hasImageDeliveryChannel)
            {
                SaveInDlcsStorage = false;
            }
            else
            {
                SaveInDlcsStorage = !(OriginIsImageServerReady && assetFromOrigin.CustomerOriginStrategy.Optimised);
            }
        }
    }
}
