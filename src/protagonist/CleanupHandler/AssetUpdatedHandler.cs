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
using Version = IIIF.ImageApi.Version;

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
    private readonly IElasticTranscoderWrapper elasticTranscoderWrapper;
    private readonly IEngineClient engineClient;
    private readonly ICleanupHandlerAssetRepository cleanupHandlerAssetRepository;


    public AssetUpdatedHandler(
        IStorageKeyGenerator storageKeyGenerator,
        IBucketWriter bucketWriter,
        IBucketReader bucketReader,
        IAssetApplicationMetadataRepository assetMetadataRepository,
        IThumbRepository thumbRepository,
        IOptions<CleanupHandlerSettings> handlerSettings,
        IElasticTranscoderWrapper elasticTranscoderWrapper,
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
        this.elasticTranscoderWrapper = elasticTranscoderWrapper;
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
        if (assetAfter == null || !message.Attributes.Keys.Contains("engineNotified") && 
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
            CleanupRolesChanged(assetBefore, assetAfter);
        }

        return true;
    }

    private void CleanupRolesChanged(Asset assetBefore, Asset assetAfter)
    {
        List<ObjectInBucket> bucketObjectsTobeRemoved = new()
        {
            storageKeyGenerator.GetInfoJsonLocation(assetAfter.Id,
                handlerSettings.AssetModifiedSettings.ImageServer.ToString(), Version.Unknown)
        };

        RemoveObjectsFromBucket(bucketObjectsTobeRemoved);
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
    
    private async Task CleanupRemoved(ImageDeliveryChannel deliveryChannel, Asset assetAfter)
    {
        switch (deliveryChannel.Channel)
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
        }
    }

    private async Task CleanupChangedPolicy(ImageDeliveryChannel newDeliveryChannel, Asset assetAfter)
    {
        switch (newDeliveryChannel.Channel)
        {
            case AssetDeliveryChannels.Image:
                CleanupChangedImageDeliveryChannel(newDeliveryChannel, assetAfter.Id);
                break;
            case AssetDeliveryChannels.Thumbnails:
                await CleanupChangedThumbnailDeliveryChannel(assetAfter);
                break;
            case AssetDeliveryChannels.Timebased:
                await CleanupChangedTimebasedDeliveryChannel(newDeliveryChannel, assetAfter.Id);
                break;
            default:
                logger.LogDebug("policy {PolicyName} does not require any changes for asset {AssetId}",
                    newDeliveryChannel.DeliveryChannelPolicy.Name, assetAfter.Id);
                break;
        }
    }

    private async Task CleanupChangedTimebasedDeliveryChannel(ImageDeliveryChannel imageDeliveryChannel, AssetId assetId)
    {
        var presetList = JsonSerializer.Deserialize<List<string>>(imageDeliveryChannel.DeliveryChannelPolicy.PolicyData);
        List<ObjectInBucket> assetsToDelete;
        var keys = new List<string>();
        var containers = new List<string>();

        foreach (var presetIdentifier in presetList)
        {
            var presetDictionary = await engineClient.GetAvPresets();
            
            if (!presetDictionary.IsNullOrEmpty() && presetDictionary.TryGetValue(presetIdentifier, out var presetName))
            {
                var presetDetails = await elasticTranscoderWrapper.GetPresetDetails(presetName);

                if (presetDetails is not null)
                {
                    var timebasedFolder = storageKeyGenerator.GetStorageLocationRoot(assetId);

                    var keysFromAws = await bucketReader.GetMatchingKeys(timebasedFolder);
                    
                    keys.AddRange(keysFromAws);
                    containers.Add(presetDetails.Container);
                }
            }
        }

        assetsToDelete = keys.Where(k =>
                !containers.Contains(k.Split('.').Last()) &&
                k.Contains(handlerSettings.AssetModifiedSettings.TimebasedKeyIndicator))
            .Select(k => new ObjectInBucket(handlerSettings.AWS.S3.StorageBucket, k)).ToList();
                    
        RemoveObjectsFromBucket(assetsToDelete);
    }

    private async Task CleanupChangedThumbnailDeliveryChannel(Asset assetAfter)
    {
        List<ObjectInBucket> bucketObjectsTobeRemoved = new()
        {
            storageKeyGenerator.GetInfoJsonLocation(assetAfter.Id,
                handlerSettings.AssetModifiedSettings.ImageServer.ToString(), Version.Unknown)
        };
        
        var thumbsToDelete = await ThumbsToBeDeleted(assetAfter);
        
        bucketObjectsTobeRemoved.AddRange(thumbsToDelete);
        RemoveObjectsFromBucket(bucketObjectsTobeRemoved);
    }

    private void CleanupChangedImageDeliveryChannel( ImageDeliveryChannel modifiedDeliveryChannel, AssetId assetId)
    {
        List<ObjectInBucket> bucketObjectsTobeRemoved = new()
        {
            storageKeyGenerator.GetInfoJsonLocation(assetId,
                handlerSettings.AssetModifiedSettings.ImageServer.ToString(), Version.Unknown)
        };

        bucketObjectsTobeRemoved.Add(modifiedDeliveryChannel.DeliveryChannelPolicyId == KnownDeliveryChannelPolicies.ImageUseOriginal
            ? storageKeyGenerator.GetStorageLocation(assetId)
            : storageKeyGenerator.GetStoredOriginalLocation(assetId));

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

    private async Task CleanupRemovedTimebasedDeliveryChannel(Asset assetBefore)
    {
        List<ObjectInBucket> bucketObjectsTobeRemoved = new()
        {
            storageKeyGenerator.GetTimebasedMetadataLocation(assetBefore.Id),
        };
        
        var timebasedFolder = storageKeyGenerator.GetStorageLocationRoot(assetBefore.Id);
        var keys = await bucketReader.GetMatchingKeys(timebasedFolder);

        foreach (var key in keys)
        {
            if (key.Contains(handlerSettings.AssetModifiedSettings.TimebasedKeyIndicator))
            {
                bucketObjectsTobeRemoved.Add(new ObjectInBucket(handlerSettings.AWS.S3.StorageBucket, key));
            }
        }

        RemoveObjectsFromBucket(bucketObjectsTobeRemoved);
    }

    private async Task CleanupRemovedThumbnailDeliveryChannel(Asset assetAfter)
    {
        List<ObjectInBucket> bucketObjectsTobeRemoved = new()
        {
            storageKeyGenerator.GetInfoJsonLocation(assetAfter.Id,
                handlerSettings.AssetModifiedSettings.ImageServer.ToString(), Version.Unknown)
        };

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
            storageKeyGenerator.GetStorageLocation(assetAfter.Id),
            storageKeyGenerator.GetInfoJsonLocation(assetAfter.Id,
                handlerSettings.AssetModifiedSettings.ImageServer.ToString(), Version.Unknown)
        };

        RegionalisedObjectInBucket? originalLocation = null;

        if (assetAfter.ImageDeliveryChannels.All(i => i.Channel != AssetDeliveryChannels.File) && 
            assetAfter.ImageDeliveryChannels.All(i => i.Channel != AssetDeliveryChannels.Thumbnails))
        {
            bucketObjectsTobeRemoved.Add(storageKeyGenerator.GetStoredOriginalLocation(assetAfter.Id));
        }

        RemoveObjectsFromBucket(bucketObjectsTobeRemoved);
    }
    
    private void RemoveObjectsFromBucket(List<ObjectInBucket> bucketObjectsTobeRemoved)
    {
        logger.LogInformation("locations to potentially be removed: {Objects}", bucketObjectsTobeRemoved);
        
        if (handlerSettings.AssetModifiedSettings.DryRun) return;

        bucketWriter.DeleteFromBucket(bucketObjectsTobeRemoved.ToArray());
    }
    
    private void RemoveObjectsFromFolderInBucket(ObjectInBucket bucketFolderToBeRemoved)
    {
        logger.LogInformation("bucket folders to potentially be removed: {Objects}", bucketFolderToBeRemoved);
        
        if (handlerSettings.AssetModifiedSettings.DryRun) return;
        
        bucketWriter.DeleteFolder(bucketFolderToBeRemoved, true);
    }
}