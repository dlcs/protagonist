using System.IO.Enumeration;
using CleanupHandler.Infrastructure;
using CleanupHandler.Infrastructure.Messages;
using CleanupHandler.Repository;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.AWS.SNS;
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
                logger.LogInformation("Asset {AssetId} not in database, aborting",
                    assetBefore.Id);
                return true;
            }
            
            logger.LogDebug("Processing update Asset notification for {AssetId}", assetBefore.Id);

            // These are used in other checks - precompute for ease
            var rolesChanged = !string.Equals(assetAfter.Roles ?? string.Empty, assetBefore.Roles ?? string.Empty,
                StringComparison.OrdinalIgnoreCase);
            var maxWidthChanged = (assetAfter.MaxWidth ?? 0) != (assetBefore.MaxWidth ?? 0);
            var openMaxWidthChanged = (assetBefore.OpenFullMax ?? 0) != (assetAfter.OpenFullMax ?? 0);

            if (NoCleanupRequired(message, rolesChanged))
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
                logger.LogInformation("Dry run enabled. Asset {AssetId} will log deletions, but not remove them",
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

            if (assetAfter.HasDeliveryChannel(AssetDeliveryChannels.Thumbnails) &&
                modifiedOrAddedChannels.All(c => c.Channel != AssetDeliveryChannels.Thumbnails) &&
                (rolesChanged || maxWidthChanged || openMaxWidthChanged))
            {
                logger.LogInformation("Thumbnail channel unchanged but MaxWidth or OpenFullMax has changed");
                await CleanupChangedThumbnail(assetAfter, s3Objects.objectsToRemove);
            }

            if (ShouldRemoveInfoJson(rolesChanged, maxWidthChanged))
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
    private static bool ShouldRemoveInfoJson(bool rolesChanged, bool maxWidthChanged) =>
        rolesChanged || maxWidthChanged;

    private static bool AssetStillIngesting(DLCS.Model.Assets.Asset assetAfter, DLCS.Model.Assets.Asset assetBefore) =>
        assetAfter.Ingesting == true && assetBefore.Finished > assetAfter.Finished;

    private static bool NoCleanupRequired(QueueMessage message, bool rolesChanged) => 
        !message.MessageAttributes.ContainsKey(ModifiedNotificationAttributes.EngineNotified) && !rolesChanged;

    private void RemoveInfoJson(DLCS.Model.Assets.Asset assetAfter, HashSet<ObjectInBucket> foldersToRemove)
    {
        logger.LogDebug("Deleting info.json files for {AssetId}", assetAfter.Id);
        var infoJsonRoot = storageKeyGenerator.GetInfoJsonRoot(assetAfter.Id);
        foldersToRemove.Add(infoJsonRoot);
    }

    private async Task CleanupModified(List<ImageDeliveryChannel> modifiedOrAdded, DLCS.Model.Assets.Asset assetBefore,
        DLCS.Model.Assets.Asset assetAfter,
        (HashSet<ObjectInBucket> objectsToRemove, HashSet<ObjectInBucket> foldersToRemove) s3Objects)
    {
        foreach (var deliveryChannel in modifiedOrAdded)
        {
            if (assetBefore.ImageDeliveryChannels.Any(x =>
                    x.Channel == deliveryChannel.Channel)) // checks for updated rather than added
            {
                await CleanupChangedPolicy(deliveryChannel, assetAfter, s3Objects.objectsToRemove);
            }
        }
    }

    private async Task CleanupRemoved(ImageDeliveryChannel deliveryChannelRemoved, DLCS.Model.Assets.Asset assetAfter, 
        (HashSet<ObjectInBucket> objectsToRemove, HashSet<ObjectInBucket> foldersToRemove) s3Objects)
    {
        logger.LogDebug("Handling deletion of {PolicyName}", deliveryChannelRemoved.DeliveryChannelPolicy?.Name);
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
                logger.LogDebug("Policy {PolicyName} does not require any changes for asset {AssetId}",
                    deliveryChannelRemoved.DeliveryChannelPolicy?.Name, assetAfter.Id);
                break;
        }
    }

    private async Task CleanupChangedPolicy(ImageDeliveryChannel deliveryChannelModified, DLCS.Model.Assets.Asset assetAfter, 
        HashSet<ObjectInBucket> objectsToRemove)
    {
        logger.LogDebug("Handling change to {PolicyName}", deliveryChannelModified.DeliveryChannelPolicy?.Name);
        switch (deliveryChannelModified.Channel)
        {
            case AssetDeliveryChannels.Image:
                CleanupChangedImageDeliveryChannel(deliveryChannelModified, assetAfter, objectsToRemove);
                break;
            case AssetDeliveryChannels.Thumbnails:
                await CleanupChangedThumbnail(assetAfter, objectsToRemove);
                break;
            case AssetDeliveryChannels.Timebased:
                await CleanupChangedTimebasedDeliveryChannel(deliveryChannelModified, assetAfter, objectsToRemove);
                break;
            default:
                logger.LogDebug("Policy {PolicyName} does not require any changes for asset {AssetId}",
                    deliveryChannelModified.DeliveryChannelPolicy?.Name, assetAfter.Id);
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

    private async Task CleanupChangedThumbnail(DLCS.Model.Assets.Asset assetAfter, HashSet<ObjectInBucket> objectsToRemove)
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
        // Get all thumb sizes based on sizes.json file in S3 - this is the index card for what the system knows about
        var thumbSizes = await thumbRepository.GetThumbnailSizes(assetAfter.Id) ?? ThumbnailSizes.Empty;
        var thumbsBucketKeys = await bucketReader.GetMatchingKeys(storageKeyGenerator.GetThumbnailsRoot(assetAfter.Id));

        var thumbnailKeysToDelete = GetThumbsToDelete(thumbsBucketKeys, thumbSizes);
        var thumbsToDelete = thumbnailKeysToDelete
            .Select(k => new ObjectInBucket(handlerSettings.AWS.S3.ThumbsBucket, k))
            .ToList();
        return thumbsToDelete;
    }
    
    private List<string> GetThumbsToDelete(string[] thumbsBucketKeys, ThumbnailSizes thumbnailSizes)
    {
        // Get the longest edge for Open and Auth thumbs, this makes comparisons simpler
        var authLongest = thumbnailSizes.Auth.Select(s => Math.Max(s[0], s[1])).ToList();
        var openLongest = thumbnailSizes.Open.Select(s => Math.Max(s[0], s[1])).ToList();
        
        // All thumbnail keys to be deleted
        var deleteList = new List<string>();
    
        // We'll only delete jpegs, filter those
        foreach (var k in thumbsBucketKeys.Where(t => FileSystemName.MatchesSimpleExpression("*.jpg", t)))
        {
            logger.LogTrace("Parsing {ThumbnailKey}", k);
            var pathParts = k.Split("/");
            
            // Guard against legacy style thumbs that aren't {longestEdge}.jpg format - these will always go
            if (!int.TryParse(pathParts[^1].Split('.')[0], out var longestEdge))
            {
                deleteList.Add(k);
                continue;
            }

            // Check gainst the "auth" or "open" sizes depending on path slug
            var toCheck = pathParts[^2] == S3StorageKeyGenerator.AuthorisedSlug ? authLongest : openLongest;
            if (!toCheck.Contains(longestEdge)) deleteList.Add(k);
        }

        return deleteList;
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
