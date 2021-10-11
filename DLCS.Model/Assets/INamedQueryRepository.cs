using System.Threading.Tasks;

namespace DLCS.Model.Assets
{
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
    }
}