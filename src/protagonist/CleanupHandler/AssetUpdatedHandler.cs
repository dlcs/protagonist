using System.IO.Enumeration;
using CleanupHandler.Infrastructure;
using CleanupHandler.Repository;
using DLCS.AWS.ElasticTranscoder;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.AWS.SQS;
using DLCS.Core.Collections;
using DLCS.Model.Assets;
using DLCS.Model.Assets.Metadata;
using DLCS.Model.Messaging;
using DLCS.Model.Policies;
using DLCS.Repository.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Packaging;

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

        var assetAfter = await cleanupHandlerAssetRepository.RetrieveAssetWithDeliveryChannels(assetBefore.Id);

        if (assetAfter == null)
        {
            logger.LogInformation("Asset {AssetId} was not found in the database for use in after calculation",
                assetBefore.Id);
            return false;
        }
        
        if (NoCleanupRequired(message, assetAfter, assetBefore)) return true;
        if (AssetStillIngesting(assetAfter, assetBefore)) return false;

        var (modifiedOrAddedChannels, removedChannels) = GetChangeSets(assetAfter, assetBefore);

        if (handlerSettings.AssetModifiedSettings.DryRun)
        {
            logger.LogInformation("Dry run enabled. Asset {AssetId} will log deletions, but not remove them",
                assetBefore.Id);
        }

        (HashSet<ObjectInBucket> objectsToRemove, HashSet<ObjectInBucket> foldersToRemove) s3Objects;
        s3Objects.objectsToRemove = new HashSet<ObjectInBucket>();
        s3Objects.foldersToRemove = new HashSet<ObjectInBucket>();
        
        if (removedChannels.Any())
        {
            foreach (var deliveryChannel in removedChannels)
            {
                await CleanupRemoved(deliveryChannel, assetAfter, s3Objects);
            }
        }
        
        if (modifiedOrAddedChannels.Any())
        {
            try
            {
                await CleanupModified(modifiedOrAddedChannels, assetBefore, assetAfter, s3Objects);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error cleaning modified delivery channels");
                return false;
            }
        }
        
        if (!Equals(assetAfter.Roles ?? string.Empty, assetBefore.Roles ?? string.Empty))
        {
            CleanupRolesChanged(assetAfter, s3Objects.foldersToRemove);
        }

        if (s3Objects.objectsToRemove.Count > 0)
        {
            await RemoveObjectsFromBucket(s3Objects.objectsToRemove);
        }

        if (s3Objects.foldersToRemove.Count > 0)
        {
            await RemoveFolderInBucket(s3Objects.foldersToRemove);
        }

        return true;
    }

    private static (List<ImageDeliveryChannel> modifiedOrAdded, List<ImageDeliveryChannel> removed) GetChangeSets(
        Asset? assetAfter, Asset assetBefore)
    {
        // Get a list of deliveryChannel changes - split by modifiedOrAdded + removed
        var modifiedOrAdded =
            assetAfter!.ImageDeliveryChannels.Where(after =>
                assetBefore.ImageDeliveryChannels.All(before =>
                    before.DeliveryChannelPolicyId != after.DeliveryChannelPolicyId ||
                    before.DeliveryChannelPolicy.Modified != after.DeliveryChannelPolicy.Modified)).ToList();
        var removed = assetBefore.ImageDeliveryChannels.Where(before =>
            assetAfter.ImageDeliveryChannels.All(after => after.Channel != before.Channel)).ToList();
        return (modifiedOrAdded, removed);
    }

    private static bool AssetStillIngesting(Asset assetAfter, Asset assetBefore)
    {
        return assetAfter.Ingesting == true && assetBefore.Finished > assetAfter.Finished;
    }
    
    private static bool NoCleanupRequired(QueueMessage message, Asset? assetAfter, Asset assetBefore)
    {
        return !message.MessageAttributes.Keys.Contains("engineNotified") &&
            (assetBefore.Roles ?? string.Empty) == (assetAfter.Roles ?? string.Empty);
    }

    private void CleanupRolesChanged(Asset assetAfter, HashSet<ObjectInBucket> foldersToRemove)
    {
        var infoJsonRoot = storageKeyGenerator.GetInfoJsonRoot(assetAfter.Id);
        foldersToRemove.Add(infoJsonRoot);
    }

    private async Task CleanupModified(List<ImageDeliveryChannel> modifiedOrAdded, Asset assetBefore, Asset assetAfter, 
        (HashSet<ObjectInBucket> objectsToRemove, HashSet<ObjectInBucket> foldersToRemove) s3Objects)
    {
        foreach (var deliveryChannel in modifiedOrAdded)
        {
            if (assetBefore.ImageDeliveryChannels.Any(x => x.Channel == deliveryChannel.Channel)) // checks for updated rather than added
            {
                await CleanupChangedPolicy(deliveryChannel, assetAfter, s3Objects.objectsToRemove);
            }
        }
    }
    
    private async Task CleanupRemoved(ImageDeliveryChannel deliveryChannelRemoved, Asset assetAfter, 
        (HashSet<ObjectInBucket> objectsToRemove, HashSet<ObjectInBucket> foldersToRemove) s3Objects)
    {
        switch (deliveryChannelRemoved.Channel)
        {
            case AssetDeliveryChannels.Image:
                CleanupRemovedImageDeliveryChannel(assetAfter, s3Objects.objectsToRemove);
                break;
            case AssetDeliveryChannels.Thumbnails:
                await CleanupRemovedThumbnailDeliveryChannel(assetAfter, s3Objects);
                break;
            case AssetDeliveryChannels.Timebased:
                await CleanupRemovedTimebasedDeliveryChannel(assetAfter, s3Objects.objectsToRemove);
                break;
            case AssetDeliveryChannels.File:
                CleanupFileDeliveryChannel(assetAfter, s3Objects.objectsToRemove);
                break;
            default:
                logger.LogDebug("policy {PolicyName} does not require any changes for asset {AssetId}",
                    deliveryChannelRemoved.DeliveryChannelPolicy.Name, assetAfter.Id);
                break;
        }
    }

    private async Task CleanupChangedPolicy(ImageDeliveryChannel deliveryChannelModified, Asset assetAfter, 
        HashSet<ObjectInBucket> objectsToRemove)
    {
        switch (deliveryChannelModified.Channel)
        {
            case AssetDeliveryChannels.Image:
                CleanupChangedImageDeliveryChannel(deliveryChannelModified, assetAfter, objectsToRemove);
                break;
            case AssetDeliveryChannels.Thumbnails:
                await CleanupChangedThumbnailDeliveryChannel(assetAfter, objectsToRemove);
                break;
            case AssetDeliveryChannels.Timebased:
                await CleanupChangedTimebasedDeliveryChannel(deliveryChannelModified, assetAfter, objectsToRemove);
                break;
            default:
                logger.LogDebug("Policy {PolicyName} does not require any changes for asset {AssetId}",
                    deliveryChannelModified.DeliveryChannelPolicy.Name, assetAfter.Id);
                break;
        }
    }

    private async Task CleanupChangedTimebasedDeliveryChannel(ImageDeliveryChannel imageDeliveryChannel,
        Asset assetAfter, HashSet<ObjectInBucket> objectsToRemove)
    {
        var presetList = imageDeliveryChannel.DeliveryChannelPolicy.AsTimebasedPresets(); 
        var keys = new List<string>();
        var extensions = new List<string>();
        var mediaPath = RetrieveMediaPath(assetAfter);
        
        var presetDictionary = await engineClient.GetAvPresets();

        if (presetDictionary.IsNullOrEmpty())
        {
            logger.LogWarning(
                "Retrieved no timebased presets from engine, {AssetId} will not be cleaned up for the timebased channel",
                assetAfter.Id);
            throw new ArgumentNullException(nameof(presetDictionary), "Failed to retrieve any preset values");
        }

        foreach (var presetIdentifier in presetList)
        {
            if (presetDictionary.TryGetValue(presetIdentifier, out var transcoderPreset))
            {
                var timebasedFolder = storageKeyGenerator.GetStorageLocationRoot(assetAfter.Id);

                var keysFromAws = await bucketReader.GetMatchingKeys(timebasedFolder);
                
                keys.AddRange(keysFromAws);
                extensions.Add(transcoderPreset.Extension);
            }
        }
        
        List<ObjectInBucket> assetsToDelete = keys.Where(k =>
                !extensions.Contains(k.Split('.').Last())  && k.Contains(mediaPath))
            .Select(k => new ObjectInBucket(handlerSettings.AWS.S3.StorageBucket, k)).ToList();
                    
       objectsToRemove.AddRange(assetsToDelete);
    }

    private async Task CleanupChangedThumbnailDeliveryChannel(Asset assetAfter, HashSet<ObjectInBucket> objectsToRemove)
    {
        var thumbsToDelete = await ThumbsToBeDeleted(assetAfter);
        objectsToRemove.AddRange(thumbsToDelete);
    }

    private void CleanupChangedImageDeliveryChannel(ImageDeliveryChannel modifiedDeliveryChannel, Asset assetAfter, 
        HashSet<ObjectInBucket> objectsToRemove)
    {
        List<ObjectInBucket> bucketObjectsToBeRemoved = new();
        
        if (modifiedDeliveryChannel.DeliveryChannelPolicyId == KnownDeliveryChannelPolicies.ImageUseOriginal)
        {
            bucketObjectsToBeRemoved.Add(storageKeyGenerator.GetStorageLocation(assetAfter.Id));
        }
        else
        {
            if (assetAfter.DoesNotHaveDeliveryChannel(AssetDeliveryChannels.File))
            {
                bucketObjectsToBeRemoved.Add(storageKeyGenerator.GetStoredOriginalLocation(assetAfter.Id));
            }
        }

        objectsToRemove.AddRange(bucketObjectsToBeRemoved);
    }

    private void CleanupFileDeliveryChannel(Asset assetAfter, HashSet<ObjectInBucket> objectsToRemove)
    {
        if (assetAfter.ImageDeliveryChannels.Any(i => i.DeliveryChannelPolicyId == KnownDeliveryChannelPolicies.ImageUseOriginal)) return;
        List<ObjectInBucket> bucketObjectsTobeRemoved = new()
        {
            storageKeyGenerator.GetStoredOriginalLocation(assetAfter.Id)
        };

        objectsToRemove.AddRange(bucketObjectsTobeRemoved);
    }

    private async Task CleanupRemovedTimebasedDeliveryChannel(Asset assetAfter, HashSet<ObjectInBucket> objectsToRemove)
    {
        List<ObjectInBucket> bucketObjectsTobeRemoved = new()
        {
            storageKeyGenerator.GetTimebasedMetadataLocation(assetAfter.Id),
        };
        
        var timebasedFolder = storageKeyGenerator.GetStorageLocationRoot(assetAfter.Id);
        var keys = await bucketReader.GetMatchingKeys(timebasedFolder);
        var path = RetrieveMediaPath(assetAfter);
        
        if (!handlerSettings.AssetModifiedSettings.DryRun)
        {
            await assetMetadataRepository.DeleteAssetApplicationMetadata(assetAfter.Id,
                AssetApplicationMetadataTypes.AVTranscodes);
        }

        foreach (var key in keys)
        {
            if (key.Contains(path))
            {
                bucketObjectsTobeRemoved.Add(new ObjectInBucket(handlerSettings.AWS.S3.StorageBucket, key));
            }
        }
        
        objectsToRemove.AddRange(bucketObjectsTobeRemoved);
    }

    private async Task CleanupRemovedThumbnailDeliveryChannel(Asset assetAfter, (HashSet<ObjectInBucket> objectsToRemove, HashSet<ObjectInBucket> foldersToRemove) s3Objects)
    {
        if (assetAfter.DoesNotHaveDeliveryChannel(AssetDeliveryChannels.Image))
        {
            s3Objects.foldersToRemove.Add(storageKeyGenerator.GetThumbnailsRoot(assetAfter.Id));

            if (!handlerSettings.AssetModifiedSettings.DryRun)
            {
                await assetMetadataRepository.DeleteAssetApplicationMetadata(assetAfter.Id,
                    AssetApplicationMetadataTypes.ThumbSizes);
            }
        }
        else
        {
            var thumbsToDelete = await ThumbsToBeDeleted(assetAfter);
            s3Objects.objectsToRemove.AddRange(thumbsToDelete);
        }
    }

    private async Task<List<ObjectInBucket>> ThumbsToBeDeleted(Asset assetAfter)
    {
        var thumbSizes = await thumbRepository.GetAllSizes(assetAfter.Id) ?? new List<int[]>();
        var thumbsBucketKeys = await bucketReader.GetMatchingKeys(storageKeyGenerator.GetThumbnailsRoot(assetAfter.Id));

        var thumbsBucketSizes = GetThumbSizesFromKeys(thumbsBucketKeys);
        var convertedThumbSizes = thumbSizes.Select(s => Math.Max(s[0], s[1]).ToString());

        var thumbsToDelete = thumbsBucketSizes.Where(t => !convertedThumbSizes.Contains(t.size))
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

    private void CleanupRemovedImageDeliveryChannel(Asset assetAfter, HashSet<ObjectInBucket> objectsToRemove)
    {
        List<ObjectInBucket> bucketObjectsTobeRemoved = new()
        {
            storageKeyGenerator.GetStorageLocation(assetAfter.Id)
        };
        
        if (assetAfter.DoesNotHaveDeliveryChannel(AssetDeliveryChannels.File))
        {
            bucketObjectsTobeRemoved.Add(storageKeyGenerator.GetStoredOriginalLocation(assetAfter.Id));
        }
        
        objectsToRemove.AddRange(bucketObjectsTobeRemoved);
    }
    
    private async Task RemoveObjectsFromBucket(HashSet<ObjectInBucket> bucketObjectsTobeRemoved)
    {
        logger.LogInformation("Locations to potentially be removed: {Objects}", bucketObjectsTobeRemoved);
        
        if (handlerSettings.AssetModifiedSettings.DryRun) return;

        await bucketWriter.DeleteFromBucket(bucketObjectsTobeRemoved.ToArray());
    }
    
    private async Task RemoveFolderInBucket(HashSet<ObjectInBucket> bucketFoldersToBeRemoved)
    {
        logger.LogInformation("Bucket folders to potentially be removed: {Objects}", bucketFoldersToBeRemoved);
        
        if (handlerSettings.AssetModifiedSettings.DryRun) return;

        foreach (var bucketFolderToBeRemoved in bucketFoldersToBeRemoved)
        {
            await bucketWriter.DeleteFolder(bucketFolderToBeRemoved, true);
        }
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
