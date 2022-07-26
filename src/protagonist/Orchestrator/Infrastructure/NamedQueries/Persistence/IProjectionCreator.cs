using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Assets;
using Orchestrator.Infrastructure.NamedQueries.Persistence.Models;

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
}