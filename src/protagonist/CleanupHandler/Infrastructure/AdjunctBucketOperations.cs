using CleanupHandler.Adjunct;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using Microsoft.Extensions.Logging;

namespace CleanupHandler.Infrastructure;

public class AdjunctBucketOperations(ILogger<AdjunctBucketOperations> logger, IStorageKeyGenerator storageKeyGenerator, IBucketWriter bucketWriter) : IAdjunctBucketOperations
{
    /// <inheritdoc />
    public async Task DeleteFromOriginBucket(DLCS.Model.Assets.Adjunct adjunct, CleanupHandlerSettings settings)
    {
        if (string.IsNullOrEmpty(settings.AWS.S3.OriginBucket))
        {
            logger.LogDebug("No OriginBucket configured - adjunct will not be deleted. {Id}", adjunct.Id);
            return;
        }

        var storageKey = storageKeyGenerator.GetStoredAdjunctLocation(adjunct.AssetId, adjunct);
        logger.LogInformation("Deleting adjunct from {StorageKey}", storageKey);
        await bucketWriter.DeleteFromBucket(storageKey);
    }
}
