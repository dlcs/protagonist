using DLCS.Model.Assets.NamedQueries;

namespace DLCS.Repository.NamedQueries.Parsing;

/// <summary>
/// Interface for parsing NamedQueries to generate <see cref="ParsedNamedQuery"/>
/// </summary>
public interface INamedQueryParser
{
    /// <summary>
    /// Generate query from specified Args and NamedQuery record
    /// </summary>
    /// <param name="customerId">Customer to run query against.</param>
    /// <param name="namedQueryArgs">Additional args for generating query object.</param>
    /// <param name="namedQueryTemplate">String representing NQ template</param>
    /// <param name="namedQueryName">The name of the NQ template</param>
    /// <returns><see cref="ParsedNamedQuery"/> object</returns>
    T GenerateParsedNamedQueryFromRequest<T>(
        int customerId,
        string? namedQueryArgs,
        string namedQueryTemplate,
        string namedQueryName)
        where T : ParsedNamedQuery;
}