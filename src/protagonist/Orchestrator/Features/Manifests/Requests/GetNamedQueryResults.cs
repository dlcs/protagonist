using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Assets.NamedQueries;
using IIIF.Presentation;
using IIIF.Serialisation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Orchestrator.Infrastructure.NamedQueries.Requests;
using Orchestrator.Models;

namespace Orchestrator.Features.Manifests.Requests;

/// <summary>
/// Mediatr request for generating manifest using a named query.
/// </summary>
public class GetNamedQueryResults : IBaseNamedQueryRequest, IRequest<DescriptionResourceResponse>
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
    private readonly IIIFNamedQueryProjector iiifNamedQueryProjector;
    private readonly NamedQueryResultGenerator namedQueryResultGenerator;
    private readonly IHttpContextAccessor httpContextAccessor;

    public GetNamedQueryResultsHandler(
        IIIFNamedQueryProjector iiifNamedQueryProjector,
        NamedQueryResultGenerator namedQueryResultGenerator,
        IHttpContextAccessor httpContextAccessor)
    {
        this.iiifNamedQueryProjector = iiifNamedQueryProjector;
        this.namedQueryResultGenerator = namedQueryResultGenerator;
        this.httpContextAccessor = httpContextAccessor;
    }

    public async Task<DescriptionResourceResponse> Handle(GetNamedQueryResults request, CancellationToken cancellationToken)
    {
        var namedQueryResult = await namedQueryResultGenerator.GetNamedQueryResult<IIIFParsedNamedQuery>(request);

        if (namedQueryResult.ParsedQuery == null) return DescriptionResourceResponse.Empty;
        if (namedQueryResult.ParsedQuery is { IsFaulty: true }) return DescriptionResourceResponse.BadRequest();

        var manifest = await iiifNamedQueryProjector.GenerateIIIFPresentation(
            namedQueryResult,
            httpContextAccessor.HttpContext.Request,
            request.IIIFPresentationVersion, cancellationToken);

        return manifest == null 
            ? DescriptionResourceResponse.Empty
            : DescriptionResourceResponse.Open(manifest);
    }
}