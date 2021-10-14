using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Collections;
using DLCS.Model.PathElements;
using DLCS.Web.Requests;
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
                await namedQueryConductor.GetNamedQueryResult(request.NamedQuery, customerPathElement,
                    request.NamedQueryArgs);

            if (namedQueryResult.Query is { IsFaulty: true }) return DescriptionResourceResponse.BadRequest();
            if (namedQueryResult.Results.IsNullOrEmpty()) return DescriptionResourceResponse.Empty;

            var manifest = iiifNamedQueryProjector.GenerateV2Manifest(namedQueryResult,
                httpContextAccessor.HttpContext.Request.GetDisplayUrl());
            return DescriptionResourceResponse.Open(manifest.AsJson());
        }
    }
}