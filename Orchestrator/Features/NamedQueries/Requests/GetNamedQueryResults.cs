using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.PathElements;
using MediatR;
using Orchestrator.Models;

namespace Orchestrator.Features.NamedQueries.Requests
{
    /// <summary>
    /// Mediatr request for generating manifest using a named query.
    /// </summary>
    public class GetNamedQueryResults : IRequest<DescriptionResourceResponse>
    {
        public string CustomerPathValue { get; }
        
        public string NamedQuery { get; }
        
        public string? NamedQueryArgs { get; }

        public GetNamedQueryResults(string customerPathValue, string namedQuery, string? namedQueryArgs)
        {
            CustomerPathValue = customerPathValue;
            NamedQuery = namedQuery;
            NamedQueryArgs = namedQueryArgs;
        }
    }
    
    public class GetNamedQueryResultsHandler : IRequestHandler<GetNamedQueryResults, DescriptionResourceResponse>
    {
        private readonly IPathCustomerRepository pathCustomerRepository;
        private readonly NamedQueryConductor namedQueryConductor;

        public GetNamedQueryResultsHandler(IPathCustomerRepository pathCustomerRepository, NamedQueryConductor namedQueryConductor)
        {
            this.pathCustomerRepository = pathCustomerRepository;
            this.namedQueryConductor = namedQueryConductor;
        }
        
        public async Task<DescriptionResourceResponse> Handle(GetNamedQueryResults request, CancellationToken cancellationToken)
        {
            var customerPathElement = await pathCustomerRepository.GetCustomer(request.CustomerPathValue);

            var images =
                await namedQueryConductor.GetNamedQueryAssetsForRequest(request.NamedQuery, customerPathElement.Id,
                    request.NamedQueryArgs);

            return DescriptionResourceResponse.Empty;
        }
    }
}