using CleanupHandler.Infrastructure;

namespace CleanupHandler.Adjunct;

public interface IAdjunctBucketOperations
{
    /// <summary>
    /// Deletes an adjunct from all S3 locations the orchestrator may serve it from:
    /// the origin bucket (optimised-origin path) and the DLCS storage bucket (engine-ingested path).
    /// </summary>
    /// <param name="adjunct">The adjunct to remove</param>
    /// <param name="settings">Settings containing bucket names</param>
    Task DeleteAdjunctStorage(DLCS.Model.Assets.Adjunct adjunct, CleanupHandlerSettings settings);
}
