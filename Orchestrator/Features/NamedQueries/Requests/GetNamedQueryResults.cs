using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Collections;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Model.PathElements;
using IIIF.Presentation;
using IIIF.Serialisation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Orchestrator.Infrastructure.NamedQueries;
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
        
        public Version IIIFPresentationVersion { get; }

        public GetNamedQueryResults(string customerPathValue, string namedQuery, string? namedQueryArgs,
            Version version)
        {
            CustomerPathValue = customerPathValue;
            NamedQuery = namedQuery;
            NamedQueryArgs = namedQueryArgs;
            IIIFPresentationVersion = version;
        }
    }
    
    public class GetNamedQueryResultsHandler : IRequestHandler<GetNamedQueryResults, DescriptionResourceResponse>
    {
        private readonly IPathCustomerRepository pathCustomerRepository;
        private readonly NamedQueryConductor namedQueryConductor;
        private readonly IIIFNamedQueryProjector iiifNamedQueryProjector;
        private readonly IHttpContextAccessor httpContextAccessor;

        public GetNamedQueryResultsHandler(
            IPathCustomerRepository pathCustomerRepository,
            NamedQueryConductor namedQueryConductor, 
            IIIFNamedQueryProjector iiifNamedQueryProjector,
            IHttpContextAccessor httpContextAccessor)
        {
            this.pathCustomerRepository = pathCustomerRepository;
            this.namedQueryConductor = namedQueryConductor;
            this.iiifNamedQueryProjector = iiifNamedQueryProjector;
            this.httpContextAccessor = httpContextAccessor;
        }

        public async Task<DescriptionResourceResponse> Handle(GetNamedQueryResults request, CancellationToken cancellationToken)
        {
            var customerPathElement = await pathCustomerRepository.GetCustomer(request.CustomerPathValue);

            var namedQueryResult =
                await namedQueryConductor.GetNamedQueryResult<IIIFParsedNamedQuery>(request.NamedQuery,
                    customerPathElement, request.NamedQueryArgs);

            if (namedQueryResult.ParsedQuery == null) return DescriptionResourceResponse.Empty;
            if (namedQueryResult.ParsedQuery is { IsFaulty: true }) return DescriptionResourceResponse.BadRequest();

            var manifest = await iiifNamedQueryProjector.GenerateIIIFPresentation(
                namedQueryResult,
                httpContextAccessor.HttpContext.Request,
                request.IIIFPresentationVersion, cancellationToken);

            return DescriptionResourceResponse.Open(manifest.AsJson());
        }
    }
}