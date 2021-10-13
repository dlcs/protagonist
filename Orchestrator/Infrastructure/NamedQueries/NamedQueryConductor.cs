using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DLCS.Model.Assets;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Model.PathElements;

namespace Orchestrator.Infrastructure.NamedQueries
{
    /// <summary>
    /// Manages orchestration of named query parameters to generate list of results.
    /// </summary>
    public class NamedQueryConductor
    {
        private readonly INamedQueryRepository namedQueryRepository;
        private readonly INamedQueryParser basicNamedQueryParser;

        public NamedQueryConductor(INamedQueryRepository namedQueryRepository, INamedQueryParser basicNamedQueryParser)
        {
            this.namedQueryRepository = namedQueryRepository;
            this.basicNamedQueryParser = basicNamedQueryParser;
        }
        
        /// <summary>
        /// Generate <see cref="NamedQueryResult"/> from named query. 
        /// </summary>
        /// <param name="queryName">Name of NQ to use</param>
        /// <param name="customerPathElement">CustomerPathElement used in request</param>
        /// <param name="args">Collection of NQ args passed in url (e.g. /2/my-images/99</param>
        public async Task<NamedQueryResult> GetNamedQueryAssetsForRequest(string queryName, 
            CustomerPathElement customerPathElement,
            string? args)
        {
            var namedQuery = await namedQueryRepository.GetByName(customerPathElement.Id, queryName);
            if (namedQuery == null)
            {
                return NamedQueryResult.Empty();
            }
            
            var assetQuery =
                basicNamedQueryParser.GenerateParsedNamedQueryFromRequest(customerPathElement, args, namedQuery.Template);
            
            var matchingImages = await namedQueryRepository.GetNamedQueryResults(assetQuery);
            return new NamedQueryResult(assetQuery, matchingImages);
        }
    }

    public class NamedQueryResult
    { 
        public ParsedNamedQuery? Query { get; }
        public IEnumerable<Asset> Results { get; private init;  }

        public static NamedQueryResult Empty()
            => new() { Results = Enumerable.Empty<Asset>() };

        private NamedQueryResult()
        {
        }

        public NamedQueryResult(ParsedNamedQuery query, IEnumerable<Asset> results)
        {
            Query = query;
            Results = results;
        }
    }
}