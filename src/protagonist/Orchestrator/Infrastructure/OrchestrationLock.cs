using System;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Threading;

namespace Orchestrator.Infrastructure;

/// <summary>
/// Thin wrapper around <see cref="AsyncKeyedLock"/>, registered as a singleton and allows internal lock to be shared
/// for orchestration purposes
/// </summary>
public class OrchestrationLock : IKeyedLock
{
    private readonly AsyncKeyedLock asyncLocker = new();

    public ILock Lock(object key, CancellationToken cancellationToken = default)
        => asyncLocker.Lock(key, cancellationToken);

    public Task<ILock> LockAsync(object key, CancellationToken cancellationToken = default)
        => asyncLocker.LockAsync(key, cancellationToken);

    public Task<ILock> LockAsync(object key, TimeSpan timeout, bool throwIfNoLock = false,
        CancellationToken cancellationToken = default)
        => asyncLocker.LockAsync(key, timeout, throwIfNoLock, cancellationToken);
}