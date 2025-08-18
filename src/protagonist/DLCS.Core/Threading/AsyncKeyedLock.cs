using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DLCS.Core.Threading;

/// <summary>
/// An asynchronous locker that locks objects by specified key.
/// Keys are statically locked, different instances of AsyncKeyedLock can hold lock for same key.
/// </summary>
/// <remarks>See https://stackoverflow.com/a/31194647/83096 </remarks>
public sealed class AsyncKeyedLock
{
    /// <summary>
    /// Synchronously get lock for key, will wait until lock is held before returning. 
    /// Prefer LockAsync over this where possible.
    /// </summary>
    /// <param name="key">Key to lock on</param>
    /// <param name="cancellationToken">async CancellationToken</param>
    /// <returns><see cref="Releaser"/> object</returns>
    public Releaser Lock(object key, CancellationToken cancellationToken = default)
    {
        GetOrCreate(key).Wait(cancellationToken);
        return new Releaser { Key = key };
    }

    /// <summary>
    /// Asynchronously get lock for key, will wait until lock is held before returning.
    /// </summary>
    /// <param name="key">Key to lock on</param>
    /// <param name="cancellationToken">async CancellationToken</param>
    /// <returns><see cref="Releaser"/> object</returns>
    public async Task<Releaser> LockAsync(object key, CancellationToken cancellationToken = default)
    {
        await GetOrCreate(key).WaitAsync(cancellationToken);
        return new Releaser { Key = key };
    }

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
    /// <returns><see cref="Releaser"/> object</returns>
    public async Task<Releaser> LockAsync(object key, TimeSpan timeout, bool throwIfNoLock = false,
        CancellationToken cancellationToken = default)
    {
        var success = await GetOrCreate(key).WaitAsync(timeout, cancellationToken);
        if (!success && throwIfNoLock)
        {
            throw new TimeoutException(
                $"Unable to attain lock for {key} within timeout of {timeout.TotalMilliseconds}ms");
        }

        return new Releaser { Key = key, HaveLock = success };
    }

    private SemaphoreSlim GetOrCreate(object key)
    {
        RefCounted<SemaphoreSlim> item;
        lock (SemaphoreSlims)
        {
            if (SemaphoreSlims.TryGetValue(key, out item))
            {
                ++item.RefCount;
            }
            else
            {
                item = new RefCounted<SemaphoreSlim>(new SemaphoreSlim(1, 1));
                SemaphoreSlims[key] = item;
            }
        }

        return item.Value;
    }

    private sealed class RefCounted<T>
    {
        public RefCounted(T value)
        {
            RefCount = 1;
            Value = value;
        }

        public int RefCount { get; set; }
        public T Value { get; }
    }

    private static readonly Dictionary<object, RefCounted<SemaphoreSlim>> SemaphoreSlims = new();

    public sealed class Releaser : IDisposable
    {
        public object Key { get; set; }

        public bool HaveLock { get; set; } = true;

        public void Dispose()
        {
            if (!HaveLock)
            {
                return;
            }

            RefCounted<SemaphoreSlim> item;
            lock (SemaphoreSlims)
            {
                item = SemaphoreSlims[Key];
                --item.RefCount;
                if (item.RefCount == 0)
                    SemaphoreSlims.Remove(Key);
            }

            item.Value.Release();
        }
    }
}