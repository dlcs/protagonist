using System.IO.Enumeration;
using CleanupHandler.Infrastructure;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.AWS.SQS;
using DLCS.Core.FileSystem;
using DLCS.Model.Assets;
using DLCS.Model.Assets.Metadata;
using DLCS.Model.Messaging;
using DLCS.Model.Policies;
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
    private readonly IFileSystem fileSystem;
    private readonly IAssetRepository assetRepository;
    private readonly IAssetApplicationMetadataRepository assetMetadataRepository;
    private readonly IThumbRepository thumbRepository;
    private readonly ILogger<AssetUpdatedHandler> logger;
    
    
    public AssetUpdatedHandler(
        IStorageKeyGenerator storageKeyGenerator,
        IBucketWriter bucketWriter,
        IBucketReader bucketReader,
        IFileSystem fileSystem,
        IAssetRepository assetRepository,
        IAssetApplicationMetadataRepository assetMetadataRepository,
        IThumbRepository thumbRepository,
        IOptions<CleanupHandlerSettings> handlerSettings,
        ILogger<AssetUpdatedHandler> logger)
    {
        this.storageKeyGenerator = storageKeyGenerator;
        this.bucketWriter = bucketWriter;
        this.bucketReader = bucketReader;
        this.fileSystem = fileSystem;
        this.handlerSettings = handlerSettings.Value;
        this.assetRepository = assetRepository;
        this.assetMetadataRepository = assetMetadataRepository;
        this.thumbRepository = thumbRepository;
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

        var assetAfter = await assetRepository.GetAsset(request.AssetBeforeUpdate.Id, true);
        
        // no changes that need to be cleaned up, or the asset has been deleted before cleanup handling
        if (assetAfter == null || !message.Attributes.Keys.Contains("engineNotified") || 
            (assetBefore.Roles ?? string.Empty) != (assetAfter.Roles ?? string.Empty))
        {
            return true;
        }
        
        // still ingesting, so just put it back on the queue
        if (assetAfter.Ingesting == true || assetBefore.Finished > assetAfter.Finished)
        {
            return false;
        }

        var modifiedOrAdded =
            assetAfter.ImageDeliveryChannels.Where(y =>
                assetBefore.ImageDeliveryChannels.All(z =>
                    z.DeliveryChannelPolicyId != y.DeliveryChannelPolicyId ||
                    z.DeliveryChannelPolicy.Modified != y.DeliveryChannelPolicy.Modified)).ToList();
        var removed = assetBefore.ImageDeliveryChannels.Where(y =>
            assetAfter.ImageDeliveryChannels.All(z => z.DeliveryChannelPolicyId != y.DeliveryChannelPolicyId)).ToList();

        var rolesChanged = assetBefore.Roles != null && assetBefore.Roles.Equals(assetAfter.Roles);
        
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
                    CleanupRemoved(deliveryChannel, assetBefore);
                }
            }
        }
        
        if (modifiedOrAdded.Any())
        {
            CleanupModified(modifiedOrAdded, assetBefore);
        }

        if (rolesChanged)
        {
            // do something
        }

        return true;
    }

    private void CleanupModified(List<ImageDeliveryChannel> modifiedOrAdded, Asset assetBefore)
    {
        foreach (var deliveryChannel in modifiedOrAdded)
        {
            if (assetBefore.ImageDeliveryChannels.Any(x => x.Channel == deliveryChannel.Channel))
            {

            }
            else
            {
                var policyModified =
                    assetBefore.ImageDeliveryChannels.First(i => i.Channel == deliveryChannel.Channel);

                if (policyModified.Id != deliveryChannel.Id)
                {
                    CleanupChangedPolicy(deliveryChannel, assetBefore);
                }
                else if (policyModified.DeliveryChannelPolicy.Modified > assetBefore.Finished)
                {
                    CleanupUpdatedPolicy(deliveryChannel, policyModified);
                }
            }
        }
    }

    private void CleanupChangedPolicy(ImageDeliveryChannel deliveryChannel, Asset assetBefore)
    {
        switch (deliveryChannel.Channel)
        {
            case AssetDeliveryChannels.Image:
                CleanupChangedImageDeliveryChannel(assetBefore);
                break;
            // case AssetDeliveryChannels.Thumbnails:
            //     await CleanupRemovedThumbnailDeliveryChannel(assetBefore);
            //     break;
            // case AssetDeliveryChannels.Timebased:
            //     CleanupRemovedTimebasedDeliveryChannel(assetBefore);
            //     break;
            // case AssetDeliveryChannels.File:
            //     CleanupRemovedFileDeliveryChannel(assetBefore);
            //     break;
        }
    }

    private void CleanupChangedImageDeliveryChannel(Asset assetBefore)
    {
        throw new NotImplementedException();
    }

    private void CleanupUpdatedPolicy(ImageDeliveryChannel deliveryChannel, ImageDeliveryChannel policyModified)
    {
        throw new NotImplementedException();
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
                CleanupRemovedFileDeliveryChannel(assetBefore);
                break;
        }
    }

    private void CleanupRemovedFileDeliveryChannel(Asset assetBefore)
    {
        if (assetBefore.ImageDeliveryChannels.All(i => i.Id != KnownDeliveryChannelPolicies.ImageUseOriginal))
        {
            List<ObjectInBucket> bucketObjectsTobeRemoved = new()
            {
                storageKeyGenerator.GetStoredOriginalLocation(assetBefore.Id)
            };
            
            logger.LogInformation("file channel removed for {AssetId}, locations to potentially be removed: {Objects}",
                assetBefore.Id, bucketObjectsTobeRemoved);

            RemoveObjectsFromBucket(bucketObjectsTobeRemoved);
        }
    }

    private void CleanupRemovedTimebasedDeliveryChannel(Asset assetBefore)
    {
        List<ObjectInBucket> bucketObjectsTobeRemoved = new()
        {
            storageKeyGenerator.GetTimebasedMetadataLocation(assetBefore.Id),
            storageKeyGenerator.GetTimebasedOutputLocation(assetBefore.Id.ToString()) // TODO: make this correct
        };
        
        logger.LogInformation("timebased channel removed for {AssetId}, locations to potentially be removed: {Objects}",
            assetBefore.Id, bucketObjectsTobeRemoved);
        
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
            await assetMetadataRepository.DeleteAssetApplicationMetadata(assetBefore.Id,
                AssetApplicationMetadataTypes.ThumbSizes);
        }
        else
        {
            var infoJsonSizes = await thumbRepository.GetAllSizes(assetBefore.Id) ?? new List<int[]>();

            var thumbsBucketKeys = await bucketReader.GetMatchingKeys(storageKeyGenerator.GetThumbnailsRoot(assetBefore.Id));

            var thumbsBucketSizes = GetThumbSizesFromKeys(thumbsBucketKeys);
            var convertedInfoJsonSizes = infoJsonSizes.Select(t => t[1].ToString());

            var thumbsToDelete = convertedInfoJsonSizes.Where(t => thumbsBucketSizes.ContainsKey(t))
                .Select(t => new ObjectInBucket(handlerSettings.AWS.S3.ThumbsBucket, t)).ToList();
            
            bucketObjectsTobeRemoved.AddRange(thumbsToDelete);
            
            RemoveObjectsFromBucket(bucketObjectsTobeRemoved);
        }
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

        logger.LogInformation("iiif-img channel removed for {AssetId}, locations to potentially be removed: {Objects}",
            assetBefore.Id, bucketObjectsTobeRemoved);

        RemoveObjectsFromBucket(bucketObjectsTobeRemoved);
    }
    
    private void RemoveObjectsFromBucket(List<ObjectInBucket> bucketObjectsTobeRemoved)
    {
        if (handlerSettings.AssetModifiedSettings.DryRun) return;

        foreach (var objectInBucket in bucketObjectsTobeRemoved)
        {
            bucketWriter.DeleteFromBucket(objectInBucket);
        }
    }
    
    private void RemoveObjectsFromFolderInBucket(ObjectInBucket bucketFolderToBeRemoved)
    {
        if (handlerSettings.AssetModifiedSettings.DryRun) return;
        
            bucketWriter.DeleteFolder(bucketFolderToBeRemoved, true);
    }
}