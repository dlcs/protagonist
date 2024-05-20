using System.IO.Enumeration;
using System.Text.Json;
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
        if (assetAfter == null || !message.Attributes.Keys.Contains("engineNotified") || 
            (assetBefore.Roles ?? string.Empty) != (assetAfter.Roles ?? string.Empty)) return true;

        // still ingesting, so just put it back on the queue
        if (assetAfter.Ingesting == true || assetBefore.Finished > assetAfter.Finished)  return false;

        var modifiedOrAdded =
            assetAfter.ImageDeliveryChannels.Where(y =>
                assetBefore.ImageDeliveryChannels.All(z =>
                    z.DeliveryChannelPolicyId != y.DeliveryChannelPolicyId ||
                    z.DeliveryChannelPolicy.Modified != y.DeliveryChannelPolicy.Modified)).ToList();
        var removed = assetBefore.ImageDeliveryChannels.Where(y =>
            assetAfter.ImageDeliveryChannels.All(z => z.DeliveryChannelPolicyId != y.DeliveryChannelPolicyId)).ToList();
        
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
                    await CleanupRemoved(deliveryChannel, assetBefore);
                }
            }
        }
        
        if (modifiedOrAdded.Any())
        {
            await CleanupModified(modifiedOrAdded, assetBefore);
        }

        if (assetBefore.Roles != null && !assetBefore.Roles.Equals(assetAfter.Roles))
        {
            await CleanupRolesChanged(assetBefore, assetAfter);
        }

        return true;
    }

    private async Task CleanupRolesChanged(Asset assetBefore, Asset assetAfter)
    {
        throw new NotImplementedException();
    }

    private async Task CleanupModified(List<ImageDeliveryChannel> modifiedOrAdded, Asset assetBefore)
    {
        foreach (var deliveryChannel in modifiedOrAdded)
        {
            if (assetBefore.ImageDeliveryChannels.Any(x => x.Channel == deliveryChannel.Channel))
            {
                var policyModified =
                    assetBefore.ImageDeliveryChannels.First(i => i.Channel == deliveryChannel.Channel);

                if (policyModified.Id != deliveryChannel.Id || policyModified.DeliveryChannelPolicy.Modified > assetBefore.Finished)
                {
                    await CleanupChangedPolicy(deliveryChannel, assetBefore);
                }
            }
        }
    }
    
    private async Task CleanupRemoved(ImageDeliveryChannel deliveryChannel, Asset assetBefore)
    {
        switch (deliveryChannel.Channel)
        {
            case AssetDeliveryChannels.Image:
                CleanupRemovedImageDeliveryChannel(assetBefore);
                break;
            case AssetDeliveryChannels.Thumbnails:
                await CleanupRemovedThumbnailDeliveryChannel(assetBefore);
                break;
            case AssetDeliveryChannels.Timebased:
                CleanupRemovedTimebasedDeliveryChannel(assetBefore);
                break;
            case AssetDeliveryChannels.File:
                CleanupFileDeliveryChannel(assetBefore);
                break;
        }
    }

    private async Task CleanupChangedPolicy(ImageDeliveryChannel newDeliveryChannel, Asset assetBefore)
    {
        switch (newDeliveryChannel.Channel)
        {
            case AssetDeliveryChannels.Image:
                CleanupChangedImageDeliveryChannel(newDeliveryChannel, assetBefore);
                break;
            case AssetDeliveryChannels.Thumbnails:
                await CleanupChangedThumbnailDeliveryChannel(assetBefore);
                break;
            case AssetDeliveryChannels.Timebased:
                CleanupChangedTimebasedDeliveryChannel(newDeliveryChannel, assetBefore);
                break;
            case AssetDeliveryChannels.File:
                CleanupFileDeliveryChannel(assetBefore);
                break;
        }
    }

    private async Task CleanupChangedTimebasedDeliveryChannel(ImageDeliveryChannel imageDeliveryChannel, Asset assetBefore)
    {
        var presetList = JsonSerializer.Deserialize<List<string>>(imageDeliveryChannel.DeliveryChannelPolicy.PolicyData);
        List<ObjectInBucket> assetsToDelete;

        foreach (var presetIdentifier in presetList)
        {
            var presetDictionary = await engineClient.GetAvPresets();
            
            if (!presetDictionary.IsNullOrEmpty() && presetDictionary.TryGetValue(presetIdentifier, out var presetName))
            {
                var presetDetails = await elasticTranscoderWrapper.GetPresetDetails(presetName);

                if (presetDetails is not null)
                {
                    var timebasedFolder = storageKeyGenerator.GetStorageLocationRoot(assetBefore.Id);

                    var keys = await bucketReader.GetMatchingKeys(timebasedFolder);

                    assetsToDelete = keys.Where(k => k.Contains($".{presetDetails.Container}"))
                        .Select(k => new ObjectInBucket(handlerSettings.AWS.S3.StorageBucket, k)).ToList();
                    
                    RemoveObjectsFromBucket(assetsToDelete);
                }
            }
        }
    }

    private async Task CleanupChangedThumbnailDeliveryChannel(Asset assetBefore)
    {
        List<ObjectInBucket> bucketObjectsTobeRemoved = new()
        {
            storageKeyGenerator.GetInfoJsonLocation(assetBefore.Id,
                handlerSettings.AssetModifiedSettings.ImageServer.ToString(), Version.Unknown)
        };
        
        var thumbsToDelete = await ThumbsToBeDeleted(assetBefore);
        
        bucketObjectsTobeRemoved.AddRange(thumbsToDelete);
        RemoveObjectsFromBucket(bucketObjectsTobeRemoved);
    }

    private void CleanupChangedImageDeliveryChannel( ImageDeliveryChannel newDeliveryChannel, Asset assetBefore)
    {
        var oldPolicy = assetBefore.ImageDeliveryChannels.First(x => x.Channel == newDeliveryChannel.Channel);
        
        List<ObjectInBucket> bucketObjectsTobeRemoved = new()
        {
            storageKeyGenerator.GetInfoJsonLocation(assetBefore.Id,
                handlerSettings.AssetModifiedSettings.ImageServer.ToString(), Version.Unknown)
        };

        if (newDeliveryChannel.Id == KnownDeliveryChannelPolicies.ImageUseOriginal)
        {
            bucketObjectsTobeRemoved.Add(storageKeyGenerator.GetStorageLocation(assetBefore.Id));
        }
        else
        {
            bucketObjectsTobeRemoved.Add(storageKeyGenerator.GetStoredOriginalLocation(assetBefore.Id));
        }

        RemoveObjectsFromBucket(bucketObjectsTobeRemoved);
    }

    private void CleanupFileDeliveryChannel(Asset assetBefore)
    {
        if (assetBefore.ImageDeliveryChannels.All(i => i.Id != KnownDeliveryChannelPolicies.ImageUseOriginal))
        {
            List<ObjectInBucket> bucketObjectsTobeRemoved = new()
            {
                storageKeyGenerator.GetStoredOriginalLocation(assetBefore.Id)
            };

            RemoveObjectsFromBucket(bucketObjectsTobeRemoved);
        }
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

    private async Task CleanupRemovedThumbnailDeliveryChannel(Asset assetBefore)
    {
        List<ObjectInBucket> bucketObjectsTobeRemoved = new()
        {
            storageKeyGenerator.GetInfoJsonLocation(assetBefore.Id,
                handlerSettings.AssetModifiedSettings.ImageServer.ToString(), Version.Unknown)
        };

        if (assetBefore.ImageDeliveryChannels.All(i => i.Channel != AssetDeliveryChannels.Image))
        {
            RemoveObjectsFromFolderInBucket(storageKeyGenerator.GetThumbnailsRoot(assetBefore.Id));

            if (!handlerSettings.AssetModifiedSettings.DryRun)
            {
                await assetMetadataRepository.DeleteAssetApplicationMetadata(assetBefore.Id,
                    AssetApplicationMetadataTypes.ThumbSizes);
            }
        }
        else
        {
            var thumbsToDelete = await ThumbsToBeDeleted(assetBefore);

            bucketObjectsTobeRemoved.AddRange(thumbsToDelete);
        }
        
        RemoveObjectsFromBucket(bucketObjectsTobeRemoved);
    }

    private async Task<List<ObjectInBucket>> ThumbsToBeDeleted(Asset assetBefore)
    {
        var infoJsonSizes = await thumbRepository.GetAllSizes(assetBefore.Id) ?? new List<int[]>();
        var thumbsBucketKeys = await bucketReader.GetMatchingKeys(storageKeyGenerator.GetThumbnailsRoot(assetBefore.Id));

        var thumbsBucketSizes = GetThumbSizesFromKeys(thumbsBucketKeys);
        var convertedInfoJsonSizes = infoJsonSizes.Select(t => t[0].ToString());

        var thumbsToDelete = convertedInfoJsonSizes.Where(t => !thumbsBucketSizes.ContainsKey(t))
            .Select(t => new ObjectInBucket(handlerSettings.AWS.S3.ThumbsBucket, t)).ToList();
        return thumbsToDelete;
    }

    private Dictionary<string, string> GetThumbSizesFromKeys(string[] thumbsBucketKeys)
    {
        var filteredFilenames = thumbsBucketKeys.Where(t => FileSystemName.MatchesSimpleExpression("*.jpg", t));

        var thumbBucketSizes = filteredFilenames
            .Select(x => new { Key = x.Split("/").Last().Split('.').First(), Value = x })
            .ToDictionary(pair => pair.Key, pair => pair.Value);

        return thumbBucketSizes;
    }

    private void CleanupRemovedImageDeliveryChannel(Asset assetBefore)
    {
        List<ObjectInBucket> bucketObjectsTobeRemoved = new()
        {
            storageKeyGenerator.GetStorageLocation(assetBefore.Id),
            storageKeyGenerator.GetInfoJsonLocation(assetBefore.Id,
                handlerSettings.AssetModifiedSettings.ImageServer.ToString(), Version.Unknown)
        };

        RegionalisedObjectInBucket? originalLocation = null;

        if (assetBefore.ImageDeliveryChannels.All(i => i.Channel == AssetDeliveryChannels.File))
        {
            bucketObjectsTobeRemoved.Add(storageKeyGenerator.GetStoredOriginalLocation(assetBefore.Id));
        }
        
        if (assetBefore.ImageDeliveryChannels.All(i => i.Channel == AssetDeliveryChannels.Thumbnails))
        {
            bucketObjectsTobeRemoved.Add(storageKeyGenerator.GetStoredOriginalLocation(assetBefore.Id));
        }

        RemoveObjectsFromBucket(bucketObjectsTobeRemoved);
    }
    
    private void RemoveObjectsFromBucket(List<ObjectInBucket> bucketObjectsTobeRemoved)
    {
        logger.LogInformation("locations to potentially be removed: {Objects}", bucketObjectsTobeRemoved);
        
        if (handlerSettings.AssetModifiedSettings.DryRun) return;

        foreach (var objectInBucket in bucketObjectsTobeRemoved)
        {
            bucketWriter.DeleteFromBucket(objectInBucket);
        }
    }
    
    private void RemoveObjectsFromFolderInBucket(ObjectInBucket bucketFolderToBeRemoved)
    {
        logger.LogInformation("bucket folders to potentially be removed: {Objects}", bucketFolderToBeRemoved);
        
        if (handlerSettings.AssetModifiedSettings.DryRun) return;
        
        bucketWriter.DeleteFolder(bucketFolderToBeRemoved, true);
    }
}