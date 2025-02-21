using System.Linq;
using System.Threading.Tasks;

namespace DLCS.Model.Assets.NamedQueries;

public interface INamedQueryRepository
{
    /// <summary>
    /// Return <see cref="NamedQuery"/> object by name 
    /// </summary>
    /// <param name="customer">Customer getting named query for</param>
    /// <param name="namedQueryName">Name of named query to fetch</param>
    /// <param name="includeGlobal">Whether to include Global named queries. If true may get a NamedQuery for
    /// another customer</param>
    /// <returns><see cref="NamedQuery"/> if found, else null</returns>
    Task<NamedQuery?> GetByName(int customer, string namedQueryName, bool includeGlobal = true);

    /// <summary>
    /// Return all assets that match criteria defined in <see cref="ParsedNamedQuery"/>
    /// </summary>
    /// <param name="query">Object containing query criteria</param>
    /// <returns>Matching assets, or empty Enumerable if no matches</returns>
    IQueryable<Asset> GetNamedQueryResults(ParsedNamedQuery query);
}