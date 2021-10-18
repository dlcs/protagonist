using System.Collections.Generic;
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
        public T? Query { get; }
        
        public IEnumerable<Asset> Results { get; private init;  }

        public static NamedQueryResult<T> Empty()
            => new() { Results = Enumerable.Empty<Asset>() };

        private NamedQueryResult()
        {
        }

        public NamedQueryResult(T query, IEnumerable<Asset> results)
        {
            Query = query;
            Results = results;
        }
    }
}