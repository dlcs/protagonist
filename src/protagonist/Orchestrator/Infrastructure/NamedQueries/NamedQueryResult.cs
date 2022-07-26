using System.Linq;
using DLCS.Model.Assets;
using DLCS.Model.Assets.NamedQueries;

namespace Orchestrator.Infrastructure.NamedQueries;

/// <summary>
/// Object containing parsed named query and all assets matching criteria.
/// </summary>
public class NamedQueryResult<T>
    where T : ParsedNamedQuery
{ 
    public T? ParsedQuery { get; private init; }
    
    public IQueryable<Asset> Results { get; private init;  }

    public static NamedQueryResult<T> Empty(T? query = null)
        => new() { ParsedQuery = query, Results = Enumerable.Empty<Asset>().AsQueryable() };

    private NamedQueryResult()
    {
    }

    public NamedQueryResult(T parsedQuery, IQueryable<Asset> results)
    {
        ParsedQuery = parsedQuery;
        Results = results;
    }
}