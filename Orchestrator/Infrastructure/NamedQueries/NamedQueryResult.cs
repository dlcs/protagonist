using System.Linq;
using DLCS.Model.Assets;
using DLCS.Model.Assets.NamedQueries;

namespace Orchestrator.Infrastructure.NamedQueries
{
    /// <summary>
    /// Object containing parsed named query and all assets matching criteria.
    /// </summary>
    public class NamedQueryResult<T>
        where T : ParsedNamedQuery
    { 
        public T? Query { get; private init; }
        
        public IQueryable<Asset> Results { get; private init;  }

        public static NamedQueryResult<T> Empty(T? query = null)
            => new() { Query = query, Results = Enumerable.Empty<Asset>().AsQueryable() };

        private NamedQueryResult()
        {
        }

        public NamedQueryResult(T query, IQueryable<Asset> results)
        {
            Query = query;
            Results = results;
        }
    }
}