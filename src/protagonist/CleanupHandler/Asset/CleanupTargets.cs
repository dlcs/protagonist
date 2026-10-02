using DLCS.AWS.S3.Models;
using DLCS.Core.Collections;

namespace CleanupHandler.Asset;

/// <summary>
/// Accumulates S3 locations to be removed by a cleanup operation. Individual objects and whole folders are tracked
/// separately as they are deleted via different bucket operations.
/// </summary>
public class CleanupTargets
{
    private readonly HashSet<ObjectInBucket> objects = [];
    private readonly HashSet<ObjectInBucket> folders = [];

    /// <summary>
    /// Individual objects to be deleted
    /// </summary>
    public IReadOnlyCollection<ObjectInBucket> Objects => objects;

    /// <summary>
    /// Folders to be deleted, along with all of their contents
    /// </summary>
    public IReadOnlyCollection<ObjectInBucket> Folders => folders;

    public void AddObject(ObjectInBucket obj) => objects.Add(obj);

    public void AddObjects(IEnumerable<ObjectInBucket> toAdd) => objects.AddRange(toAdd);

    public void AddFolder(ObjectInBucket folder) => folders.Add(folder);
}
