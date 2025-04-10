﻿using CleanupHandler.Infrastructure;
using CleanupHandler.Repository;
using DLCS.AWS.Cloudfront;
using DLCS.AWS.S3;
using DLCS.AWS.SQS;
using DLCS.Core.Collections;
using DLCS.Core.Exceptions;
using DLCS.Core.FileSystem;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Messaging;
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
    private readonly ICleanupHandlerAssetRepository assetRepository;

    public AssetDeletedHandler(
        IStorageKeyGenerator storageKeyGenerator,
        IBucketWriter bucketWriter,
        ICacheInvalidator cacheInvalidator,
        IFileSystem fileSystem,
        IOptions<CleanupHandlerSettings> handlerSettings,
        ICleanupHandlerAssetRepository assetRepository,
        ILogger<AssetDeletedHandler> logger)
    {
        this.storageKeyGenerator = storageKeyGenerator;
        this.bucketWriter = bucketWriter;
        this.fileSystem = fileSystem;
        this.cacheInvalidator = cacheInvalidator;
        this.logger = logger;
        this.handlerSettings = handlerSettings.Value;
        this.assetRepository = assetRepository;
    }
    
    public async Task<bool> HandleMessage(QueueMessage message, CancellationToken cancellationToken = default)
    {
        AssetDeletedNotificationRequest? request;
        try
        {
            request = message.GetMessageContents<AssetDeletedNotificationRequest>();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to deserialize asset {@Message}", message);
            return false;
        }

        if (request?.Asset?.Id == null) return false;

        logger.LogDebug("Processing delete notification for {AssetId}", request.Asset.Id);
        
        // if the item exists in the db, assume the asset has been reingested after delete
        if (await assetRepository.CheckExists(request.Asset.Id))
        {
            logger.LogInformation("asset {Asset} can be found in the database, so will not be deleted", request.Asset.Id);
            return true;
        }

        await DeleteThumbnails(request.Asset.Id);
        await DeleteTileOptimised(request.Asset.Id);
        DeleteFromNas(request.Asset.Id);
        await DeleteFromOriginBucket(request.Asset.Id);

        if (request.DeleteFrom.HasFlag(ImageCacheType.Cdn))
        {
            return await InvalidateContentDeliveryNetwork(request.Asset, request.CustomerPathElement.Name);
        }

        logger.LogDebug("cdn invalidation not specified for {Asset}", request.Asset.Id);
        return true;
    }

    private async Task DeleteFromOriginBucket(AssetId assetId)
    {
        if (string.IsNullOrEmpty(handlerSettings.AWS.S3.OriginBucket))
        {
            logger.LogDebug("No OriginBucket configured - origin folder will not be deleted. {AssetId}", assetId);
            return;
        }

        var storageKey = storageKeyGenerator.GetOriginRoot(assetId);
        logger.LogInformation("Deleting OriginBucket key from {StorageKey} for {AssetId}", storageKey, assetId);
        await bucketWriter.DeleteFolder(storageKey, true);
    }

    private async Task<bool> InvalidateContentDeliveryNetwork(Asset asset, string customerName)
    {
        if (string.IsNullOrEmpty(handlerSettings.AWS.Cloudfront.DistributionId))
        {
            logger.LogDebug("No Cloudfront distribution id configured - Cloudfront will not be invalidated");
            return true;
        }
        
        var invalidationUriList = new List<string>();
        var idList = new List<string>()
        {
            asset.Id.ToString(),
            $"{customerName}/{asset.Id.Space}/{asset.Id.Asset}"
        };
        
        if (!asset.ImageDeliveryChannels.IsNullOrEmpty())
        {
            invalidationUriList = SetDeliveryChannelInvalidations(asset.Id, 
                asset.ImageDeliveryChannels, idList);
        }
        else if (asset.Family.HasValue)
        {
            if(asset.Family == AssetFamily.Image)
            {
                logger.LogDebug(
                    "Received message body with no 'deliveryChannels' property - using 'family' as a fallback. {@Request}",
                    asset);
                foreach (var id in idList)
                {
                    invalidationUriList.Add($"/iiif-manifest/{id}");
                    invalidationUriList.Add($"/iiif-manifest/v2/{id}");
                    invalidationUriList.Add($"/iiif-manifest/v3/{id}");
                }
            }
        }
        else
        {
            logger.LogDebug("Unable to set invalidations - 'deliveryChannels' and 'family' not found in message body. {@Request}",
                asset); 
        }
        
        if (invalidationUriList.Count > 0)
        {
            return await cacheInvalidator.InvalidateCdnCache(invalidationUriList);
        }

        return true;
    }

    private static List<string> SetDeliveryChannelInvalidations(AssetId assetId, ICollection<ImageDeliveryChannel> deliveryChannels,
        List<string> idList)
    {
        var invalidationUriList = new List<string>();

        foreach (var deliveryChannel in deliveryChannels)
        {
            foreach (var id in idList)
            {
                switch (deliveryChannel.Channel)
                {
                    case AssetDeliveryChannels.Image:
                        invalidationUriList.Add($"/iiif-img/{id}/*");
                        invalidationUriList.Add($"/iiif-img/v2/{id}/*");
                        invalidationUriList.Add($"/iiif-img/v3/{id}/*");
                        invalidationUriList.Add($"/thumbs/{id}/*");
                        invalidationUriList.Add($"/thumbs/v2/{id}/*");
                        invalidationUriList.Add($"/thumbs/v3/{id}/*");
                        invalidationUriList.Add($"/iiif-manifest/{id}");
                        invalidationUriList.Add($"/iiif-manifest/v2/{id}");
                        invalidationUriList.Add($"/iiif-manifest/v3/{id}");
                        break;
                    case AssetDeliveryChannels.File:
                        invalidationUriList.Add($"/file/{id}");
                        break;
                    case AssetDeliveryChannels.Timebased:
                        invalidationUriList.Add($"/iiif-av/{id}/*");
                        break;
                }
            }
        }

        return invalidationUriList;
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
        await bucketWriter.DeleteFolder(thumbsRoot, true);
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
        await bucketWriter.DeleteFolder(storageKey, true);
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
