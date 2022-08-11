using System;

namespace DLCS.Core.Threading;

/// <summary>
/// Lock for specified key
/// </summary>
public interface ILock : IDisposable
{
    /// <summary>
    /// Key for lock
    /// </summary>
    object Key { get; set; }

    /// <summary>
    /// If true, this is the current lock.
    /// Can be false if other requests may already have lock (e.g. timeout surpassed)
    /// </summary>
    bool ExclusiveLock { get; set; }
}