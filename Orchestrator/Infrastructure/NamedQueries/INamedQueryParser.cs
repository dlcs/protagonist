using DLCS.Model.Assets.NamedQueries;
using DLCS.Model.PathElements;

namespace Orchestrator.Infrastructure.NamedQueries
{
    /// <summary>
    /// Interface for parsing NamedQueries to generate <see cref="ParsedNamedQuery"/>
    /// </summary>
    public interface INamedQueryParser
    {
        /// <summary>
        /// Generate query from specified Args and NamedQuery record
        /// </summary>
        /// <param name="customerPathElement">Customer to run query against.</param>
        /// <param name="namedQueryArgs">Additional args for generating query object.</param>
        /// <param name="namedQueryTemplate">String representing NQ template</param>
        /// <returns><see cref="ParsedNamedQuery"/> object</returns>
        T GenerateParsedNamedQueryFromRequest<T>(
            CustomerPathElement customerPathElement,
            string? namedQueryArgs,
            string namedQueryTemplate)
            where T : ParsedNamedQuery;
    }
}