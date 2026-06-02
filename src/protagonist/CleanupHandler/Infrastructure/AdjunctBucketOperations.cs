using CleanupHandler.Adjunct;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using Microsoft.Extensions.Logging;

namespace CleanupHandler.Infrastructure;

public class AdjunctBucketOperations(ILogger<AdjunctBucketOperations> logger, IStorageKeyGenerator storageKeyGenerator, IBucketWriter bucketWriter) : IAdjunctBucketOperations
{
    /// <inheritdoc />
    public async Task DeleteAdjunctStorage(DLCS.Model.Assets.Adjunct adjunct, CleanupHandlerSettings settings)
    {
        // The key is identical in both buckets: {assetId}/adjuncts/{adjunctId}
        var storageKey = storageKeyGenerator.GetStoredAdjunctLocation(adjunct.AssetId, adjunct);

        var toDelete = new List<ObjectInBucket>();

        if (!string.IsNullOrEmpty(settings.AWS.S3.StorageBucket))
        {
            toDelete.Add(storageKey);
        }
        else
        {
            logger.LogDebug("No StorageBucket configured - adjunct will not be deleted from storage. {Id}", adjunct.Id);
        }

        if (!string.IsNullOrEmpty(settings.AWS.S3.OriginBucket))
        {
            toDelete.Add(new ObjectInBucket(settings.AWS.S3.OriginBucket, storageKey.Key));
        }
        else
        {
            logger.LogDebug("No OriginBucket configured - adjunct will not be deleted from origin. {Id}", adjunct.Id);
        }

        if (toDelete.Count == 0) return;

        logger.LogInformation("Deleting adjunct {Id} from {Count} bucket(s)", adjunct.Id, toDelete.Count);
        await bucketWriter.DeleteFromBucket(toDelete.ToArray());
    }
}
