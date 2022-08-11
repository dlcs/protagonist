using System;
using System.Threading;
using System.Threading.Tasks;

namespace DLCS.Core.Threading;

public interface IKeyedLock
{
    /// <summary>
    /// Synchronously get lock for key, will wait until lock is held before returning. 
    /// Prefer LockAsync over this where possible.
    /// </summary>
    /// <param name="key">Key to lock on</param>
    /// <param name="cancellationToken">async CancellationToken</param>
    /// <returns><see cref="AsyncKeyedLock.Releaser"/> object</returns>
    ILock Lock(object key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously get lock for key, will wait until lock is held before returning.
    /// </summary>
    /// <param name="key">Key to lock on</param>
    /// <param name="cancellationToken">async CancellationToken</param>
    /// <returns><see cref="AsyncKeyedLock.Releaser"/> object</returns>
    Task<ILock> LockAsync(object key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously get lock for key, will wait until lock is held, or timeout is reached, before returning.
    /// </summary>
    /// <param name="key">Key to lock on</param>
    /// <param name="timeout">A <see cref="TimeSpan"/> that represents the number of milliseconds to wait, a
    /// <see cref="TimeSpan"/> that represents -1 milliseconds to wait indefinitely, or a <see cref="TimeSpan"/>
    /// that represents 0 ms to test wait handle and return immediately.</param>
    /// <param name="throwIfNoLock">If true, a <see cref="TimeoutException"/> is thrown if lock not attained
    /// within timeout. If false and timeout exceeded, a Releaser object is returned but lock not attained.</param>
    /// <param name="cancellationToken">async CancellationToken</param>
    /// <returns><see cref="AsyncKeyedLock.Releaser"/> object</returns>
    Task<ILock> LockAsync(object key, TimeSpan timeout, bool throwIfNoLock = false,
        CancellationToken cancellationToken = default);
}