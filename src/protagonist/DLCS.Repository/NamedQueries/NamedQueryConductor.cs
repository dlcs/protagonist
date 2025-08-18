using System.Threading.Tasks;
using DLCS.Model.Assets.NamedQueries;
using Microsoft.Extensions.Logging;

namespace DLCS.Repository.NamedQueries;

/// <summary>
/// Manages orchestration of named query parameters to generate list of results.
/// </summary>
public class NamedQueryConductor
{
    private readonly INamedQueryRepository namedQueryRepository;
    private readonly NamedQueryParserResolver namedQueryParserResolver;
    private readonly ILogger<NamedQueryConductor> logger;

    public NamedQueryConductor(INamedQueryRepository namedQueryRepository, 
        NamedQueryParserResolver namedQueryParserResolver,
        ILogger<NamedQueryConductor> logger)
    {
        this.namedQueryRepository = namedQueryRepository; 
        this.namedQueryParserResolver = namedQueryParserResolver;
        this.logger = logger;
    }

    /// <summary>
    /// Generate <see cref="NamedQueryResult{T}"/> from named query. 
    /// </summary>
    /// <param name="queryName">Name of NQ to use</param>
    /// <param name="customerId">CustomerId for NQ</param>
    /// <param name="args">Collection of NQ args passed in url (e.g. /2/my-images/99</param>
    public async Task<NamedQueryResult<T>> GetNamedQueryResult<T>(string queryName,
        int customerId, string? args)
        where T : ParsedNamedQuery
    {
        var namedQuery = await namedQueryRepository.GetByName(customerId, queryName);
        if (namedQuery == null)
        {
            logger.LogDebug("Could not find NQ with name {NamedQueryName} for customer {Customer}",
                queryName, customerId);
            return NamedQueryResult<T>.Empty();
        }

        var parsedNamedQuery = ParseNamedQuery<T>(customerId, args, namedQuery);
        if (parsedNamedQuery.IsFaulty)
        {
            logger.LogInformation("Error parsing NQ for {QueryName} with {QueryArgs}", queryName, args);
            return NamedQueryResult<T>.Empty(parsedNamedQuery);
        }

        var matchingImages = namedQueryRepository.GetNamedQueryResults(parsedNamedQuery);
        return new NamedQueryResult<T>(parsedNamedQuery, matchingImages);
    }

    private T ParseNamedQuery<T>(int customerId, string? args, NamedQuery? namedQuery)
        where T : ParsedNamedQuery
    {
        var namedQueryParser = namedQueryParserResolver(NamedQueryTypeDeriver.GetNamedQueryParser<T>());
        var parsedNamedQuery =
            namedQueryParser.GenerateParsedNamedQueryFromRequest<T>(customerId, args, namedQuery.Template,
                namedQuery.Name);
        return parsedNamedQuery;
    }
}