using System.Threading.Tasks;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Model.PathElements;

namespace Orchestrator.Infrastructure.NamedQueries.Requests
{
    /// <summary>
    /// Class responsible for parsing CustomerPathElement and getting NamedQueryResult.
    /// </summary>
    public class NamedQueryResultGenerator
    { 
        private readonly IPathCustomerRepository pathCustomerRepository;
        private readonly NamedQueryConductor namedQueryConductor;
        
        public NamedQueryResultGenerator(
            IPathCustomerRepository pathCustomerRepository,
            NamedQueryConductor namedQueryConductor)
        {
            this.pathCustomerRepository = pathCustomerRepository;
            this.namedQueryConductor = namedQueryConductor;
        }

        public async Task<NamedQueryResult<T>> GetNamedQueryResult<T>(IBaseNamedQueryRequest request)
            where T : ParsedNamedQuery
        {
            var customerPathElement = await pathCustomerRepository.GetCustomer(request.CustomerPathValue);

            var namedQueryResult =
                await namedQueryConductor.GetNamedQueryResult<T>(request.NamedQuery,
                    customerPathElement, request.NamedQueryArgs);
            return namedQueryResult;
        }
    }
}