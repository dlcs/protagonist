using CleanupHandler.Infrastructure;

namespace CleanupHandler.Adjunct;

public interface IAdjunctBucketOperations
{
    /// <summary>
    /// Deletes an adjunct from the origin bucket
    /// </summary>
    /// <param name="adjunct">The adjunct to remove</param>
    /// <param name="settings">Settings containing the location of the origin bucket</param>
    Task DeleteFromOriginBucket(DLCS.Model.Assets.Adjunct adjunct, CleanupHandlerSettings settings);
}
