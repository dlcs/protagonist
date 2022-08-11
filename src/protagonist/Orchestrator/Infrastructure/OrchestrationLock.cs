using System;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Threading;
using DLCS.Core.Types;

namespace Orchestrator.Infrastructure;

/// <summary>
/// Thin wrapper around <see cref="AsyncKeyedLock"/>, registered as a singleton and allows internal lock to be shared
/// for orchestration purposes
/// </summary>
public class OrchestrationLock
{
    private readonly AsyncKeyedLock asyncLocker = new();

    private static string GetLockKey(AssetId assetId) => $"orch:{assetId}";

    public ILock Lock(AssetId assetId, CancellationToken cancellationToken = default)
        => asyncLocker.Lock(GetLockKey(assetId), cancellationToken);

    public Task<ILock> LockAsync(AssetId assetId, CancellationToken cancellationToken = default)
        => asyncLocker.LockAsync(GetLockKey(assetId), cancellationToken);

    public Task<ILock> LockAsync(AssetId assetId, TimeSpan timeout, bool throwIfNoLock = false,
        CancellationToken cancellationToken = default)
        => asyncLocker.LockAsync(GetLockKey(assetId), timeout, throwIfNoLock, cancellationToken);
}