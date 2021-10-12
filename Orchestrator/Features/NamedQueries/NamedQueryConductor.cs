using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DLCS.Model.Assets;
using DLCS.Model.Assets.NamedQueries;

namespace Orchestrator.Features.NamedQueries
{
    public class NamedQueryConductor
    {
        private readonly INamedQueryRepository namedQueryRepository;
        private readonly INamedQueryParser basicNamedQueryParser;

        public NamedQueryConductor(INamedQueryRepository namedQueryRepository, INamedQueryParser basicNamedQueryParser)
        {
            this.namedQueryRepository = namedQueryRepository;
            this.basicNamedQueryParser = basicNamedQueryParser;
        }
        
        public async Task<IEnumerable<Asset>> GetNamedQueryAssetsForRequest(string queryName, int customer, string? args)
        {
            var namedQuery = await namedQueryRepository.GetByName(customer, queryName);
            if (namedQuery == null)
            {
                return Enumerable.Empty<Asset>();
            }

            // Populate the ResourceMappedAssetQuery object using template + query args
            var assetQuery =
                basicNamedQueryParser.GenerateResourceMappedAssetQueryFromRequest(customer, args, namedQuery.Template);

            // Get the images that match NQ results
            var images = await namedQueryRepository.GetNamedQueryResults(assetQuery);
            return images;
        }
    }
}