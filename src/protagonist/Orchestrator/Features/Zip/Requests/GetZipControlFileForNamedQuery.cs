using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Repository.NamedQueries;
using DLCS.Repository.NamedQueries.Models;
using MediatR;
using Orchestrator.Infrastructure.NamedQueries.Requests;

namespace Orchestrator.Features.Zip.Requests;

/// <summary>
/// Mediatr request for getting zip control-file for named query
/// </summary>
public class GetZipControlFileForNamedQuery : IBaseNamedQueryRequest, IRequest<ControlFile?>
{
    public string CustomerPathValue { get; }

    public string NamedQuery { get; }

    public string? NamedQueryArgs { get; }

    public GetZipControlFileForNamedQuery(string customerPathValue, string namedQuery, string? namedQueryArgs)
    {
        CustomerPathValue = customerPathValue;
        NamedQuery = namedQuery;
        NamedQueryArgs = namedQueryArgs;
    }
}

public class GetZipControlFileForNamedQueryHandler : IRequestHandler<GetZipControlFileForNamedQuery, ControlFile?>
{
    private readonly NamedQueryStorageService namedQueryStorageService;
    private readonly NamedQueryResultGenerator namedQueryResultGenerator;

    public GetZipControlFileForNamedQueryHandler(
        NamedQueryStorageService namedQueryStorageService,
        NamedQueryResultGenerator namedQueryResultGenerator)
    {
        this.namedQueryStorageService = namedQueryStorageService;
        this.namedQueryResultGenerator = namedQueryResultGenerator;
    }
    
    public async Task<ControlFile?> Handle(GetZipControlFileForNamedQuery request, CancellationToken cancellationToken)
    {
        var resultContainer = await namedQueryResultGenerator.GetNamedQueryResult<ZipParsedNamedQuery>(request);
        var namedQueryResult = resultContainer.NamedQueryResult;

        if (namedQueryResult.ParsedQuery is null or { IsFaulty: true }) return null;

        var controlFile = await namedQueryStorageService.GetControlFile(namedQueryResult.ParsedQuery, cancellationToken);
        return controlFile;
    }
}