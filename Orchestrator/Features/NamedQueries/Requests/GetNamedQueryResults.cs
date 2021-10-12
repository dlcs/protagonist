using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Collections;
using DLCS.Model.PathElements;
using IIIF.Serialisation;
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
        private readonly IIIFNamedQueryProjector iiifNamedQueryProjector;

        public GetNamedQueryResultsHandler(IPathCustomerRepository pathCustomerRepository,
            NamedQueryConductor namedQueryConductor, IIIFNamedQueryProjector iiifNamedQueryProjector)
        {
            this.pathCustomerRepository = pathCustomerRepository;
            this.namedQueryConductor = namedQueryConductor;
            this.iiifNamedQueryProjector = iiifNamedQueryProjector;
        }

        public async Task<DescriptionResourceResponse> Handle(GetNamedQueryResults request, CancellationToken cancellationToken)
        {
            var customerPathElement = await pathCustomerRepository.GetCustomer(request.CustomerPathValue);

            var namedQueryResult =
                await namedQueryConductor.GetNamedQueryAssetsForRequest(request.NamedQuery, customerPathElement,
                    request.NamedQueryArgs);
            
            if (namedQueryResult.Results.IsNullOrEmpty()) return DescriptionResourceResponse.Empty;

            var manifest = iiifNamedQueryProjector.GenerateV2Manifest(namedQueryResult);
            return DescriptionResourceResponse.Open(manifest.AsJson());
        }
    }
}