using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Assets;
using DLCS.Repository.NamedQueries.Models;

namespace Orchestrator.Infrastructure.NamedQueries.Persistence;

/// <summary>
/// Basic interface for creating and storing NamedQuery projection
/// </summary>
/// <typeparam name="T"></typeparam>
public interface IProjectionCreator<in T>
{
    /// <summary>
    /// Create projection and store in object store for later retrieval
    /// </summary>
    Task<(bool success, ControlFile? controlFile)> PersistProjection(T parsedNamedQuery, List<Asset> images,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark the <see cref="ControlFile"/> for parsedNamedQuery as being completed (ie item exists)
    /// </summary>
    Task MarkControlFileComplete(T parsedNamedQuery, ControlFile controlFile, long fileSize);
}
