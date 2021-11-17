using System.Collections.Generic;
using System.Threading.Tasks;
using DLCS.Model.Assets;

namespace Orchestrator.Infrastructure.NamedQueries.Persistence
{
    public interface IProjectionCreator<in T>
    {
        Task<bool> PersistProjection(T parsedNamedQuery, List<Asset> images);
    }
}