using System.Text.Json.Nodes;
using CleanupHandler.Infrastructure;
using DLCS.AWS.Cloudfront;
using DLCS.AWS.S3;
using DLCS.AWS.SQS;
using DLCS.Core.Exceptions;
using DLCS.Core.FileSystem;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Templates;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CleanupHandler;

/// <summary>
/// Handler for SQS messages notifying of asset deletion
/// </summary>
public class AssetDeletedHandler : IMessageHandler
{
    private readonly CleanupHandlerSettings handlerSettings;
    private readonly IStorageKeyGenerator storageKeyGenerator;
    private readonly IBucketWriter bucketWriter;
    private readonly IFileSystem fileSystem;
    private readonly ILogger<AssetDeletedHandler> logger;
    private readonly ICacheInvalidator cacheInvalidator;

    public AssetDeletedHandler(
        IStorageKeyGenerator storageKeyGenerator,
        IBucketWriter bucketWriter,
        ICacheInvalidator cacheInvalidator,
        IFileSystem fileSystem,
        IOptions<CleanupHandlerSettings> handlerSettings,
        ILogger<AssetDeletedHandler> logger)
    {
        this.storageKeyGenerator = storageKeyGenerator;
        this.bucketWriter = bucketWriter;
        this.fileSystem = fileSystem;
        this.cacheInvalidator = cacheInvalidator;
        this.logger = logger;
        this.handlerSettings = handlerSettings.Value;
    }
    
    public async Task<bool> HandleMessage(QueueMessage message, CancellationToken cancellationToken = default)
    {
        var assetId = TryGetAssetId(message);
        if (assetId == null) return true;

        logger.LogDebug("Processing delete notification for {AssetId}", assetId);

        await DeleteThumbnails(assetId);
        await DeleteTileOptimised(assetId);
        DeleteFromNas(assetId);
        await DeleteFromOriginBucket(assetId);
        await DeleteFromOutputBucket(assetId);

        await InvalidateContentDeliveryNetwork(message.Body, assetId);

        return true;
    }

    private async Task DeleteFromOriginBucket(AssetId assetId)
    {
        if (string.IsNullOrEmpty(handlerSettings.AWS.S3.OriginBucket))
        {
            logger.LogDebug("No OriginBucket configured - origin folder will not be deleted. {AssetId}", assetId);
            return;
        }

        var storageKey = storageKeyGenerator.GetOriginBucketRoot(assetId);
        logger.LogInformation("Deleting OriginBucket key from {StorageKey} for {AssetId}", storageKey, assetId);
        await bucketWriter.DeleteFolder(storageKey);
    }
    
    private async Task DeleteFromOutputBucket(AssetId assetId)
    {
        if (string.IsNullOrEmpty(handlerSettings.AWS.S3.OutputBucket))
        {
            logger.LogDebug("No OutputBucket configured - output folder will not be deleted. {AssetId}", assetId);
            return;
        }

        var storageKey = storageKeyGenerator.GetOutputBucketRoot(assetId);
        logger.LogInformation("Deleting OutputBucket key from {StorageKey} for {AssetId}", storageKey, assetId);
        await bucketWriter.DeleteFolder(storageKey);
    }

    private async Task InvalidateContentDeliveryNetwork(JsonObject messageBody, AssetId assetId)
    {
        if (string.IsNullOrEmpty(handlerSettings.AWS.Cloudfront.DistributionId))
        {
            logger.LogDebug("No Cloudfront distribution id configured - Cloudfront will not be invalidated");
            return;
        }
        
        if (!messageBody.TryGetPropertyValue("deliveryChannels", out var deliveryChannelsProperty))
        {
            logger.LogWarning("Received message body with no 'deliveryChannels' property - Cloudfront will not be invalidated. {Body}", 
                messageBody.ToJsonString());
            return;
        }

        List<string> invalidationUriList = new List<string>();
        foreach (var deliveryChannel in deliveryChannelsProperty!.AsArray())
        {
            switch (deliveryChannel!.ToString())
            {
                case AssetDeliveryChannels.Image:
                    invalidationUriList.Add($"/iiif-img/{assetId}/*");
                    invalidationUriList.Add($"/iiif-img/v2/{assetId}/*");
                    invalidationUriList.Add($"/iiif-img/v3/{assetId}/*");
                    invalidationUriList.Add($"/thumbs/{assetId}/*");
                    invalidationUriList.Add($"/thumbs/v2/{assetId}/*");
                    invalidationUriList.Add($"/thumbs/v3/{assetId}/*");
                    break;
                case AssetDeliveryChannels.File: 
                    invalidationUriList.Add($"/file/{assetId}/*");
                    break;
                case AssetDeliveryChannels.Timebased: 
                    invalidationUriList.Add($"/iiif-av/{assetId}/*");
                    break;
            }
        }

        await cacheInvalidator.InvalidateCdnCache(invalidationUriList);
    }

    private async Task DeleteThumbnails(AssetId assetId)
    {
        if (string.IsNullOrEmpty(handlerSettings.AWS.S3.ThumbsBucket))
        {
            logger.LogDebug("No thumbsBucket configured - thumbnails will not be deleted. {AssetId}", assetId);
            return;
        }

        var thumbsRoot = storageKeyGenerator.GetThumbnailsRoot(assetId);
        logger.LogInformation("Deleting thumbs from {ThumbnailRoot} for {AssetId}", thumbsRoot, assetId);
        await bucketWriter.DeleteFolder(thumbsRoot);
    }
    
    private async Task DeleteTileOptimised(AssetId assetId)
    {
        if (string.IsNullOrEmpty(handlerSettings.AWS.S3.StorageBucket))
        {
            logger.LogDebug("No storageBucket configured - tile-optimised derivative will not be deleted. {AssetId}",
                assetId);
            return;
        }

        var storageKey = storageKeyGenerator.GetStorageLocationRoot(assetId);
        logger.LogInformation("Deleting tile-optimised key from {StorageKey} for {AssetId}", storageKey, assetId);
        await bucketWriter.DeleteFolder(storageKey);
    }
    
    private void DeleteFromNas(AssetId assetId)
    {
        if (string.IsNullOrEmpty(handlerSettings.ImageFolderTemplate))
        {
            logger.LogDebug("No ImageFolderTemplate configured - NAS file will not be deleted. {AssetId}", assetId);
            return;
        }

        var imagePath = TemplatedFolders.GenerateFolderTemplate(handlerSettings.ImageFolderTemplate, assetId);
        logger.LogInformation("Deleting file: {StorageKey} for {AssetId}", imagePath, assetId);
        fileSystem.DeleteFile(imagePath);
    }

    private AssetId? TryGetAssetId(QueueMessage message)
    {
        var messageBody = message.GetMessageContents();
        if (messageBody == null)
        {
            logger.LogWarning("Received message but unable to parse contents. {Body}", message.Body.ToJsonString());
            return null;
        }
        
        if (!messageBody.TryGetPropertyValue("id", out var idProperty))
        {
            logger.LogWarning("Received message body with no 'id' property. {Body}", message.Body.ToJsonString());
            return null;
        }

        try
        {
            return AssetId.FromString(idProperty!.GetValue<string>());
        }
        catch (InvalidAssetIdException assetIdEx)
        {
            logger.LogError(assetIdEx, "Unable to process delete notification as assetId is not valid");
        }
        
        return null;
    }
}