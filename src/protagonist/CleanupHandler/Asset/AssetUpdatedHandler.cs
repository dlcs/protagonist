using System.IO.Enumeration;
using CleanupHandler.Infrastructure;
using CleanupHandler.Infrastructure.Messages;
using CleanupHandler.Repository;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.AWS.SQS;
using DLCS.AWS.Transcoding;
using DLCS.Core.Collections;
using DLCS.Model.Assets;
using DLCS.Model.Assets.Metadata;
using DLCS.Model.Messaging;
using DLCS.Model.Policies;
using DLCS.Repository.Messaging;
using DLCS.Web.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Packaging;

namespace CleanupHandler.Asset;

/// <summary>
/// Handle 'asset modified' notifications - running various derivative cleanup operations depending on what has changed
/// </summary>
public class AssetUpdatedHandler(
    IStorageKeyGenerator storageKeyGenerator,
    IBucketWriter bucketWriter,
    IBucketReader bucketReader,
    IAssetApplicationMetadataRepository assetMetadataRepository,
    IThumbRepository thumbRepository,
    IOptions<CleanupHandlerSettings> handlerSettings,
    IEngineClient engineClient,
    ICleanupHandlerAssetRepository cleanupHandlerAssetRepository,
    ILogger<AssetUpdatedHandler> logger)
    : IMessageHandler
{
    private readonly CleanupHandlerSettings handlerSettings = handlerSettings.Value;

    public async Task<bool> HandleMessage(QueueMessage message, CancellationToken cancellationToken = default)
    {
        var request = TryParseMessage(message);
        if (request == null) return false;

        using (LogContextHelpers.SetCorrelationId(message.MessageId))
        {
            var assetBefore = request.DeliverableBeforeUpdate!;

            var assetAfter = await cleanupHandlerAssetRepository.RetrieveAssetWithDeliveryChannels(assetBefore.Id);

            if (assetAfter == null)
            {
                logger.LogInformation("Asset {AssetId} was not found in the database for use in after calculation",
                    assetBefore.Id);
                return false;
            }
            
            logger.LogDebug("Processing update DLCS.Model.Assets.Asset notification for {AssetId}", assetBefore.Id);

            if (NoCleanupRequired(message, assetAfter, assetBefore))
            {
                logger.LogDebug("No cleanup required, aborting");
                return true;
            }

            if (AssetStillIngesting(assetAfter, assetBefore))
            {
                logger.LogDebug("Asset {AssetId} still ingesting - aborting", assetBefore.Id);
                return false;
            } 

            var (modifiedOrAddedChannels, removedChannels) =
                ChangeCalculator.GetChannelChangeSets(assetAfter, assetBefore);

            if (handlerSettings.AssetModifiedSettings.DryRun)
            {
                logger.LogInformation("Dry run enabled. DLCS.Model.Assets.Asset {AssetId} will log deletions, but not remove them",
                    assetBefore.Id);
            }

            (HashSet<ObjectInBucket> objectsToRemove, HashSet<ObjectInBucket> foldersToRemove) s3Objects;
            s3Objects.objectsToRemove = [];
            s3Objects.foldersToRemove = [];

            if (removedChannels.Count != 0)
            {
                foreach (var deliveryChannel in removedChannels)
                {
                    await CleanupRemoved(deliveryChannel, assetAfter, s3Objects);
                }
            }

            if (modifiedOrAddedChannels.Count != 0)
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

            if (ShouldRemoveInfoJson(assetAfter, assetBefore))
            {
                RemoveInfoJson(assetAfter, s3Objects.foldersToRemove);
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
    }

    private DeliverableUpdatedNotification<DLCS.Model.Assets.Asset>? TryParseMessage(QueueMessage message)
    {
        var updateMessage = MessageParser.TryParseUpdatedMessage<DLCS.Model.Assets.Asset>(message, logger);

        // this is legacy handling for the older message format - it should be removed at the point this code is released everywhere
        // and just the above line used
        if (updateMessage == null)
        {
            logger.LogInformation("Message not parsed in the new format.  Attempting legacy parsing");
            
            try
            {
                var request = message.GetMessageContents<AssetUpdatedNotificationRequest>();

                if (request?.AssetBeforeUpdate?.Id == null)
                {
                    logger.LogInformation("Deserialised message but no 'before' DLCS.Model.Assets.Asset id found");
                    return null;
                }
                return request.ConvertToStandard();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to deserialize notification {@Message}", message);
                return null;
            }
        }

        return updateMessage;
    }

    // If a value has changed that can affect info.json we need to replace it
    private static bool ShouldRemoveInfoJson(DLCS.Model.Assets.Asset assetAfter, DLCS.Model.Assets.Asset assetBefore)
    {
        var rolesChanged = !string.Equals(assetAfter.Roles ?? string.Empty, assetBefore.Roles ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);

        var maxWidthChanged = (assetAfter.MaxWidth ?? 0) != (assetBefore.MaxWidth ?? 0);
        return rolesChanged || maxWidthChanged;
    }

    private static bool AssetStillIngesting(DLCS.Model.Assets.Asset assetAfter, DLCS.Model.Assets.Asset assetBefore) =>
        assetAfter.Ingesting == true && assetBefore.Finished > assetAfter.Finished;

    private static bool NoCleanupRequired(QueueMessage message, DLCS.Model.Assets.Asset assetAfter, DLCS.Model.Assets.Asset assetBefore)
    {
        return !message.MessageAttributes.ContainsKey("engineNotified") &&
            (assetBefore.Roles ?? string.Empty) == (assetAfter.Roles ?? string.Empty);
    }

    private void RemoveInfoJson(DLCS.Model.Assets.Asset assetAfter, HashSet<ObjectInBucket> foldersToRemove)
    {
        logger.LogDebug("Deleting info.json files for {AssetId}", assetAfter.Id);
        var infoJsonRoot = storageKeyGenerator.GetInfoJsonRoot(assetAfter.Id);
        foldersToRemove.Add(infoJsonRoot);
    }

    private async Task CleanupModified(List<ImageDeliveryChannel> modifiedOrAdded, DLCS.Model.Assets.Asset assetBefore, DLCS.Model.Assets.Asset assetAfter, 
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
    
    private async Task CleanupRemoved(ImageDeliveryChannel deliveryChannelRemoved, DLCS.Model.Assets.Asset assetAfter, 
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
                logger.LogDebug("policy {PolicyName} does not require any changes for DLCS.Model.Assets.Asset {AssetId}",
                    deliveryChannelRemoved.DeliveryChannelPolicy.Name, assetAfter.Id);
                break;
        }
    }

    private async Task CleanupChangedPolicy(ImageDeliveryChannel deliveryChannelModified, DLCS.Model.Assets.Asset assetAfter, 
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
                logger.LogDebug("Policy {PolicyName} does not require any changes for DLCS.Model.Assets.Asset {AssetId}",
                    deliveryChannelModified.DeliveryChannelPolicy.Name, assetAfter.Id);
                break;
        }
    }

    private async Task CleanupChangedTimebasedDeliveryChannel(ImageDeliveryChannel imageDeliveryChannel,
        DLCS.Model.Assets.Asset assetAfter, HashSet<ObjectInBucket> objectsToRemove)
    {
        logger.LogDebug("Processing timebased delivery-channel change");
        var presetList = imageDeliveryChannel.DeliveryChannelPolicy.AsTimebasedPresets(); 
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
                extensions.Add(transcoderPreset.Extension);
            }
            else
            {
                throw new ArgumentNullException(nameof(presetIdentifier),
                    $"Failed to retrieve preset {presetIdentifier}");
            }
        }

        var timebasedFolder = storageKeyGenerator.GetStorageLocationRoot(assetAfter.Id);
        var keys = await bucketReader.GetMatchingKeys(timebasedFolder);
        
        List<ObjectInBucket> assetsToDelete = keys.Where(k =>
                !extensions.Contains(k.Split('.').Last()) && k.Contains(mediaPath))
            .Select(k => new ObjectInBucket(handlerSettings.AWS.S3.StorageBucket, k)).ToList();
                    
       objectsToRemove.AddRange(assetsToDelete);
    }

    private async Task CleanupChangedThumbnailDeliveryChannel(DLCS.Model.Assets.Asset assetAfter, HashSet<ObjectInBucket> objectsToRemove)
    {
        var thumbsToDelete = await ThumbsToBeDeleted(assetAfter);
        objectsToRemove.AddRange(thumbsToDelete);
    }

    private void CleanupChangedImageDeliveryChannel(ImageDeliveryChannel modifiedDeliveryChannel, DLCS.Model.Assets.Asset assetAfter, 
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

    private void CleanupFileDeliveryChannel(DLCS.Model.Assets.Asset assetAfter, HashSet<ObjectInBucket> objectsToRemove)
    {
        if (assetAfter.ImageDeliveryChannels.Any(i => i.DeliveryChannelPolicyId == KnownDeliveryChannelPolicies.ImageUseOriginal)) return;
        List<ObjectInBucket> bucketObjectsTobeRemoved = [storageKeyGenerator.GetStoredOriginalLocation(assetAfter.Id)];

        objectsToRemove.AddRange(bucketObjectsTobeRemoved);
    }

    private async Task CleanupRemovedTimebasedDeliveryChannel(DLCS.Model.Assets.Asset assetAfter, HashSet<ObjectInBucket> objectsToRemove)
    {
        List<ObjectInBucket> bucketObjectsTobeRemoved =
        [
            storageKeyGenerator.GetTimebasedMetadataLocation(assetAfter.Id)
        ];
        
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

    private async Task CleanupRemovedThumbnailDeliveryChannel(DLCS.Model.Assets.Asset assetAfter, (HashSet<ObjectInBucket> objectsToRemove, HashSet<ObjectInBucket> foldersToRemove) s3Objects)
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

    private async Task<List<ObjectInBucket>> ThumbsToBeDeleted(DLCS.Model.Assets.Asset assetAfter)
    {
        var thumbSizes = await thumbRepository.GetAllSizes(assetAfter.Id) ?? [];
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

    private void CleanupRemovedImageDeliveryChannel(DLCS.Model.Assets.Asset assetAfter, HashSet<ObjectInBucket> objectsToRemove)
    {
        List<ObjectInBucket> bucketObjectsTobeRemoved = [storageKeyGenerator.GetStorageLocation(assetAfter.Id)];
        
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
    
    private static string RetrieveMediaPath(DLCS.Model.Assets.Asset asset)
    {
        var template = TranscoderTemplates.GetDestinationTemplate(asset.MediaType!);
        var path = template
            .Replace("{asset}", S3StorageKeyGenerator.GetStorageKey(asset.Id))
            .Replace(".{extension}", string.Empty);
        return path;
    }
}

public static class ChangeCalculator
{
    /// <summary>
    /// Compare the 'before' and 'after' assets to derive a list of changes in <see cref="ImageDeliveryChannel"/>s
    /// between then 
    /// </summary>
    public static (List<ImageDeliveryChannel> modifiedOrAdded, List<ImageDeliveryChannel> removed) GetChannelChangeSets(
        DLCS.Model.Assets.Asset assetAfter, DLCS.Model.Assets.Asset assetBefore)
    {
        // Get a list of deliveryChannel changes - split by modifiedOrAdded + removed
        var modifiedOrAdded =
            assetAfter.ImageDeliveryChannels.Where(after =>
                assetBefore.ImageDeliveryChannels.All(before =>
                    before.DeliveryChannelPolicyId != after.DeliveryChannelPolicyId ||
                    assetBefore.Finished < after.DeliveryChannelPolicy.Modified)).ToList();
        var removed = assetBefore.ImageDeliveryChannels.Where(before =>
            assetAfter.ImageDeliveryChannels.All(after => after.Channel != before.Channel)).ToList();
        return (modifiedOrAdded, removed);
    }
}
