using System;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Repository.NamedQueries;
using DLCS.Repository.NamedQueries.Models;
using MediatR;
using Newtonsoft.Json;
using Orchestrator.Infrastructure.NamedQueries.Requests;

namespace Orchestrator.Features.PDF.Requests;

/// <summary>
/// Mediatr request for getting PDF control-file for named query
/// </summary>
public class GetPdfControlFileForNamedQuery : IBaseNamedQueryRequest, IRequest<PdfControlFile?>
{
    public string CustomerPathValue { get; }

    public string NamedQuery { get; }

    public string? NamedQueryArgs { get; }

    public GetPdfControlFileForNamedQuery(string customerPathValue, string namedQuery, string? namedQueryArgs)
    {
        CustomerPathValue = customerPathValue;
        NamedQuery = namedQuery;
        NamedQueryArgs = namedQueryArgs;
    }
}

public class GetPdfControlFileForNamedQueryHandler : IRequestHandler<GetPdfControlFileForNamedQuery, PdfControlFile?>
{
    private readonly NamedQueryStorageService namedQueryStorageService;
    private readonly NamedQueryResultGenerator namedQueryResultGenerator;

    public GetPdfControlFileForNamedQueryHandler(
        NamedQueryStorageService namedQueryStorageService,
        NamedQueryResultGenerator namedQueryResultGenerator)
    {
        this.namedQueryStorageService = namedQueryStorageService;
        this.namedQueryResultGenerator = namedQueryResultGenerator;
    }
    
    public async Task<PdfControlFile?> Handle(GetPdfControlFileForNamedQuery request, CancellationToken cancellationToken)
    {
        var resultContainer = await namedQueryResultGenerator.GetNamedQueryResult<PdfParsedNamedQuery>(request);
        var namedQueryResult = resultContainer.NamedQueryResult;

        if (namedQueryResult.ParsedQuery is null or { IsFaulty: true }) return null;

        var controlFile =
            await namedQueryStorageService.GetControlFile<PdfControlFile>(namedQueryResult.ParsedQuery,
                cancellationToken);
        return controlFile ?? new PdfControlFile(ControlFile.Empty);
    }
}

public class PdfControlFile : ControlFile
{
    /// <summary>
    /// This is for backwards compatibility as "itemCount" property was previously "pageCount". From when
    /// Deliverator only supported PDF projections
    /// </summary>
    [Obsolete("Use itemCount instead")]
    [JsonProperty("pageCount")]
    public int? PageCount
    {
        get => pageCount ?? ItemCount;
        set => pageCount = value;
    }

    private int? pageCount;

    public PdfControlFile()
    {
    }
    
    public PdfControlFile(ControlFile controlFile)
    {
        Key = controlFile.Key;
        Exists = controlFile.Exists;
        InProcess = controlFile.InProcess;
        Created = controlFile.Created;
        ItemCount = controlFile.ItemCount;
        SizeBytes = controlFile.SizeBytes;
        Roles = controlFile.Roles;
    }
}