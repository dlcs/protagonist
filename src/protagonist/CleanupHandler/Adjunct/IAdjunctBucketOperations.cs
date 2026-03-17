using CleanupHandler.Infrastructure;

namespace CleanupHandler.Adjunct;

public interface IAdjunctBucketOperations
{
    Task DeleteFromOriginBucket(DLCS.Model.Assets.Adjunct adjunct, CleanupHandlerSettings settings);
}
