using System.IO.Enumeration;
using System.Text.Json;
using CleanupHandler.Infrastructure;
using CleanupHandler.Repository;
using DLCS.AWS.ElasticTranscoder;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.AWS.SQS;
using DLCS.Core.Collections;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Assets.Metadata;
using DLCS.Model.Messaging;
using DLCS.Model.Policies;
using DLCS.Repository.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CleanupHandler;

public class AssetUpdatedHandler  : IMessageHandler
{
    private readonly CleanupHandlerSettings handlerSettings;
    private readonly IStorageKeyGenerator storageKeyGenerator;
    private readonly IBucketWriter bucketWriter;
    private readonly IBucketReader bucketReader;
    private readonly IAssetApplicationMetadataRepository assetMetadataRepository;
    private readonly IThumbRepository thumbRepository;
    private readonly ILogger<AssetUpdatedHandler> logger;
    private readonly IEngineClient engineClient;
    private readonly ICleanupHandlerAssetRepository cleanupHandlerAssetRepository;


    public AssetUpdatedHandler(
        IStorageKeyGenerator storageKeyGenerator,
        IBucketWriter bucketWriter,
        IBucketReader bucketReader,
        IAssetApplicationMetadataRepository assetMetadataRepository,
        IThumbRepository thumbRepository,
        IOptions<CleanupHandlerSettings> handlerSettings,
        IEngineClient engineClient,
        ICleanupHandlerAssetRepository cleanupHandlerAssetRepository,
        ILogger<AssetUpdatedHandler> logger)
    {
        this.storageKeyGenerator = storageKeyGenerator;
        this.bucketWriter = bucketWriter;
        this.bucketReader = bucketReader;
        this.handlerSettings = handlerSettings.Value;
        this.assetMetadataRepository = assetMetadataRepository;
        this.thumbRepository = thumbRepository;
        this.engineClient = engineClient;
        this.cleanupHandlerAssetRepository = cleanupHandlerAssetRepository;
        this.logger = logger;
    }

    public async Task<bool> HandleMessage(QueueMessage message, CancellationToken cancellationToken = default)
    {
        AssetUpdatedNotificationRequest? request;
        try
        {
            request = message.GetMessageContents<AssetUpdatedNotificationRequest>();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to deserialize asset {@Message}", message);
            return false;
        }

        if (request?.AssetBeforeUpdate?.Id == null) return false;

        var assetBefore = request.AssetBeforeUpdate;

        var assetAfter = cleanupHandlerAssetRepository.RetrieveAssetWithDeliveryChannels(assetBefore.Id);
        
        // no changes that need to be cleaned up, or the asset has been deleted before cleanup handling
        if (assetAfter == null || !message.MessageAttributes.Keys.Contains("engineNotified") && 
            (assetBefore.Roles ?? string.Empty) == (assetAfter.Roles ?? string.Empty)) return true;

        // still ingesting, so just put it back on the queue
        if (assetAfter.Ingesting == true || assetBefore.Finished > assetAfter.Finished)  return false;

        var modifiedOrAdded =
            assetAfter.ImageDeliveryChannels.Where(y =>
                assetBefore.ImageDeliveryChannels.All(z =>
                    z.DeliveryChannelPolicyId != y.DeliveryChannelPolicyId ||
                    z.DeliveryChannelPolicy.Modified != y.DeliveryChannelPolicy.Modified)).ToList();
        var removed = assetBefore.ImageDeliveryChannels.Where(y =>
            assetAfter.ImageDeliveryChannels.All(z => z.Channel != y.Channel)).ToList();
        
        if (handlerSettings.AssetModifiedSettings.DryRun)
        {
            logger.LogInformation("Dry run enabled. Asset {AssetId} will log deletions, but not remove them",
                assetBefore.Id);
        }
        
        if (removed.Any())
        {
            foreach (var deliveryChannel in removed)
            {
                if (assetAfter.ImageDeliveryChannels.All(x => x.Channel != deliveryChannel.Channel))
                {
                    await CleanupRemoved(deliveryChannel, assetAfter);
                }
            }
        }
        
        if (modifiedOrAdded.Any())
        {
            await CleanupModified(modifiedOrAdded, assetBefore, assetAfter);
        }

        if (assetBefore.Roles != null && !assetBefore.Roles.Equals(assetAfter.Roles))
        {
            CleanupRolesChanged(assetAfter);
        }

        return true;
    }

    private void CleanupRolesChanged(Asset assetAfter)
    {
        var infoJsonRoot = storageKeyGenerator.GetInfoJsonRoot(assetAfter.Id);

        RemoveObjectsFromFolderInBucket(infoJsonRoot);
    }

    private async Task CleanupModified(List<ImageDeliveryChannel> modifiedOrAdded, Asset assetBefore, Asset assetAfter)
    {
        foreach (var deliveryChannel in modifiedOrAdded)
        {
            if (assetBefore.ImageDeliveryChannels.Any(x => x.Channel == deliveryChannel.Channel)) // checks for updated rather than added
            {
                await CleanupChangedPolicy(deliveryChannel, assetAfter);
            }
        }
    }
    
    private async Task CleanupRemoved(ImageDeliveryChannel deliveryChannelRemoved, Asset assetAfter)
    {
        switch (deliveryChannelRemoved.Channel)
        {
            case AssetDeliveryChannels.Image:
                CleanupRemovedImageDeliveryChannel(assetAfter);
                break;
            case AssetDeliveryChannels.Thumbnails:
                await CleanupRemovedThumbnailDeliveryChannel(assetAfter);
                break;
            case AssetDeliveryChannels.Timebased:
                await CleanupRemovedTimebasedDeliveryChannel(assetAfter);
                break;
            case AssetDeliveryChannels.File:
                CleanupFileDeliveryChannel(assetAfter);
                break;
            default:
                logger.LogDebug("policy {PolicyName} does not require any changes for asset {AssetId}",
                    deliveryChannelRemoved.DeliveryChannelPolicy.Name, assetAfter.Id);
                break;
        }
    }

    private async Task CleanupChangedPolicy(ImageDeliveryChannel deliveryChannelModified, Asset assetAfter)
    {
        switch (deliveryChannelModified.Channel)
        {
            case AssetDeliveryChannels.Image:
                CleanupChangedImageDeliveryChannel(deliveryChannelModified, assetAfter.Id);
                break;
            case AssetDeliveryChannels.Thumbnails:
                await CleanupChangedThumbnailDeliveryChannel(assetAfter);
                break;
            case AssetDeliveryChannels.Timebased:
                await CleanupChangedTimebasedDeliveryChannel(deliveryChannelModified, assetAfter);
                break;
            default:
                logger.LogDebug("policy {PolicyName} does not require any changes for asset {AssetId}",
                    deliveryChannelModified.DeliveryChannelPolicy.Name, assetAfter.Id);
                break;
        }
    }

    private async Task CleanupChangedTimebasedDeliveryChannel(ImageDeliveryChannel imageDeliveryChannel, Asset assetAfter)
    {
        var presetList = JsonSerializer.Deserialize<List<string>>(imageDeliveryChannel.DeliveryChannelPolicy.PolicyData);
        List<ObjectInBucket> assetsToDelete;
        var keys = new List<string>();
        var extensions = new List<string>();
        var mediaPath = RetrieveMediaPath(assetAfter);
        
        var presetDictionary = await engineClient.GetAvPresets();

        foreach (var presetIdentifier in presetList)
        {
            if (!presetDictionary.IsNullOrEmpty() && presetDictionary.TryGetValue(presetIdentifier, out var transcoderPreset))
            {
                var timebasedFolder = storageKeyGenerator.GetStorageLocationRoot(assetAfter.Id);

                var keysFromAws = await bucketReader.GetMatchingKeys(timebasedFolder);
                
                keys.AddRange(keysFromAws);
                extensions.Add(transcoderPreset.Extension);
            }
        }
        
        assetsToDelete = keys.Where(k =>
                !extensions.Contains(k.Split('.').Last())  && k.Contains(mediaPath))
            .Select(k => new ObjectInBucket(handlerSettings.AWS.S3.StorageBucket, k)).ToList();
                    
        await RemoveObjectsFromBucket(assetsToDelete);
    }

    private async Task CleanupChangedThumbnailDeliveryChannel(Asset assetAfter)
    {
        List<ObjectInBucket> bucketObjectsTobeRemoved = new();
        
        var thumbsToDelete = await ThumbsToBeDeleted(assetAfter);
        
        bucketObjectsTobeRemoved.AddRange(thumbsToDelete);
        await RemoveObjectsFromBucket(bucketObjectsTobeRemoved);
    }

    private void CleanupChangedImageDeliveryChannel( ImageDeliveryChannel modifiedDeliveryChannel, AssetId assetId)
    {
        List<ObjectInBucket> bucketObjectsTobeRemoved = new()
        {
            modifiedDeliveryChannel.DeliveryChannelPolicyId == KnownDeliveryChannelPolicies.ImageUseOriginal
                ? storageKeyGenerator.GetStorageLocation(assetId)
                : storageKeyGenerator.GetStoredOriginalLocation(assetId)
        };

        RemoveObjectsFromBucket(bucketObjectsTobeRemoved);
    }

    private void CleanupFileDeliveryChannel(Asset assetAfter)
    {
        if (assetAfter.ImageDeliveryChannels.Any(i => i.DeliveryChannelPolicyId == KnownDeliveryChannelPolicies.ImageUseOriginal)) return;
        List<ObjectInBucket> bucketObjectsTobeRemoved = new()
        {
            storageKeyGenerator.GetStoredOriginalLocation(assetAfter.Id)
        };

        RemoveObjectsFromBucket(bucketObjectsTobeRemoved);
    }

    private async Task CleanupRemovedTimebasedDeliveryChannel(Asset assetAfter)
    {
        List<ObjectInBucket> bucketObjectsTobeRemoved = new()
        {
            storageKeyGenerator.GetTimebasedMetadataLocation(assetAfter.Id),
        };
        
        var timebasedFolder = storageKeyGenerator.GetStorageLocationRoot(assetAfter.Id);
        var keys = await bucketReader.GetMatchingKeys(timebasedFolder);
        var path = RetrieveMediaPath(assetAfter);

        foreach (var key in keys)
        {
            if (key.Contains(path))
            {
                bucketObjectsTobeRemoved.Add(new ObjectInBucket(handlerSettings.AWS.S3.StorageBucket, key));
            }
        }

        await RemoveObjectsFromBucket(bucketObjectsTobeRemoved);
    }

    private async Task CleanupRemovedThumbnailDeliveryChannel(Asset assetAfter)
    {
        List<ObjectInBucket> bucketObjectsTobeRemoved = new();

        if (assetAfter.ImageDeliveryChannels.All(i => i.Channel != AssetDeliveryChannels.Image))
        {
            RemoveObjectsFromFolderInBucket(storageKeyGenerator.GetThumbnailsRoot(assetAfter.Id));

            if (!handlerSettings.AssetModifiedSettings.DryRun)
            {
                await assetMetadataRepository.DeleteAssetApplicationMetadata(assetAfter.Id,
                    AssetApplicationMetadataTypes.ThumbSizes);
            }
        }
        else
        {
            var thumbsToDelete = await ThumbsToBeDeleted(assetAfter);

            bucketObjectsTobeRemoved.AddRange(thumbsToDelete);
        }
        
        RemoveObjectsFromBucket(bucketObjectsTobeRemoved);
    }

    private async Task<List<ObjectInBucket>> ThumbsToBeDeleted(Asset assetAfter)
    {
        var infoJsonSizes = await thumbRepository.GetAllSizes(assetAfter.Id) ?? new List<int[]>();
        var thumbsBucketKeys = await bucketReader.GetMatchingKeys(storageKeyGenerator.GetThumbnailsRoot(assetAfter.Id));

        var thumbsBucketSizes = GetThumbSizesFromKeys(thumbsBucketKeys);
        var convertedInfoJsonSizes = infoJsonSizes.Select(t => t[0].ToString());

        var thumbsToDelete = thumbsBucketSizes.Where(t => !convertedInfoJsonSizes.Contains(t.size))
            .Select(t => new ObjectInBucket(handlerSettings.AWS.S3.ThumbsBucket, t.path)).ToList();
        
        return thumbsToDelete;
    }

    private List<(string size, string path)> GetThumbSizesFromKeys(string[] thumbsBucketKeys)
    {
        var filteredFilenames = thumbsBucketKeys.Where(t => FileSystemName.MatchesSimpleExpression("*.jpg", t));

        var thumbBucketSizes = filteredFilenames
            .Select(f => (f.Split("/").Last().Split('.').First(), f)).ToList();

        return thumbBucketSizes;
    }

    private void CleanupRemovedImageDeliveryChannel(Asset assetAfter)
    {
        List<ObjectInBucket> bucketObjectsTobeRemoved = new()
        {
            storageKeyGenerator.GetStorageLocation(assetAfter.Id)
        };
        
        if (assetAfter.ImageDeliveryChannels.All(i => i.Channel != AssetDeliveryChannels.File) && 
            assetAfter.ImageDeliveryChannels.All(i => i.Channel != AssetDeliveryChannels.Thumbnails))
        {
            bucketObjectsTobeRemoved.Add(storageKeyGenerator.GetStoredOriginalLocation(assetAfter.Id));
        }

        RemoveObjectsFromBucket(bucketObjectsTobeRemoved);
    }
    
    private async Task RemoveObjectsFromBucket(List<ObjectInBucket> bucketObjectsTobeRemoved)
    {
        logger.LogInformation("locations to potentially be removed: {Objects}", bucketObjectsTobeRemoved);
        
        if (handlerSettings.AssetModifiedSettings.DryRun) return;

        await bucketWriter.DeleteFromBucket(bucketObjectsTobeRemoved.ToArray());
    }
    
    private async Task RemoveObjectsFromFolderInBucket(ObjectInBucket bucketFolderToBeRemoved)
    {
        logger.LogInformation("bucket folders to potentially be removed: {Objects}", bucketFolderToBeRemoved);
        
        if (handlerSettings.AssetModifiedSettings.DryRun) return;
        
        await bucketWriter.DeleteFolder(bucketFolderToBeRemoved, true);
    }
    
    private static string RetrieveMediaPath(Asset asset)
    {
        var template = TranscoderTemplates.GetDestinationTemplate(asset.MediaType!);
        var path = template
            .Replace("{jobId}/", "")
            .Replace("{asset}", S3StorageKeyGenerator.GetStorageKey(asset.Id))
            .Replace(".{extension}", "");
        return path;
    }
}