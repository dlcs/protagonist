using CleanupHandler.Infrastructure;
using DLCS.AWS.S3;
using DLCS.AWS.SQS;
using DLCS.Core.FileSystem;
using DLCS.Model.Assets;
using DLCS.Model.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CleanupHandler;

public class AssetUpdatedHandler  : IMessageHandler
{
    private readonly CleanupHandlerSettings handlerSettings;
    private readonly IStorageKeyGenerator storageKeyGenerator;
    private readonly IBucketWriter bucketWriter;
    private readonly IFileSystem fileSystem;
    private readonly IAssetRepository assetRepository;
    private readonly ILogger<AssetUpdatedHandler> logger;
    
    
    public AssetUpdatedHandler(
        IStorageKeyGenerator storageKeyGenerator,
        IBucketWriter bucketWriter,
        IFileSystem fileSystem,
        IAssetRepository assetRepository,
        IOptions<CleanupHandlerSettings> handlerSettings,
        ILogger<AssetUpdatedHandler> logger)
    {
        this.storageKeyGenerator = storageKeyGenerator;
        this.bucketWriter = bucketWriter;
        this.fileSystem = fileSystem;
        this.handlerSettings = handlerSettings.Value;
        this.assetRepository = assetRepository;
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
        var assetAfter = await assetRepository.GetAsset(request.AssetBeforeUpdate.Id);
        
        // no changes that need to be cleaned up, or the asset has been deleted before cleanup handling
        if (assetAfter == null || !message.Attributes.Keys.Contains("engineNotified") || 
            (assetBefore.Roles ?? string.Empty) != (assetAfter.Roles ?? string.Empty) || assetAfter.Ingesting == true)
        {
            return true;
        }

        var modifiedOrAdded =
            assetAfter.ImageDeliveryChannels.IntersectBy(assetBefore.ImageDeliveryChannels.Select(x => x.Id),
                x => x.Id);
        var removed = assetBefore.ImageDeliveryChannels.IntersectBy(assetAfter.ImageDeliveryChannels.Select(x => x.Id),
            x => x.Id);

        if (!modifiedOrAdded.Any() || !removed.Any())
        {
            return true;
        }
        
        // still ingesting, so just put it back on the queue
        if (assetAfter.Ingesting == true || assetBefore.Finished > assetAfter.Finished)
        {
            return false;
        }
        
        
        return true;
    }
}