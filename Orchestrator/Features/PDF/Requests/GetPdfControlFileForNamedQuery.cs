using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Assets.NamedQueries;
using MediatR;
using Orchestrator.Infrastructure.NamedQueries.Requests;

namespace Orchestrator.Features.PDF.Requests
{
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
        private readonly StoredNamedQueryService storedNamedQueryService;
        private readonly NamedQueryResultGenerator namedQueryResultGenerator;

        public GetPdfControlFileForNamedQueryHandler(
            StoredNamedQueryService storedNamedQueryService,
            NamedQueryResultGenerator namedQueryResultGenerator)
        {
            this.storedNamedQueryService = storedNamedQueryService;
            this.namedQueryResultGenerator = namedQueryResultGenerator;
        }
        
        public async Task<PdfControlFile?> Handle(GetPdfControlFileForNamedQuery request, CancellationToken cancellationToken)
        {
            var namedQueryResult = await namedQueryResultGenerator.GetNamedQueryResult<PdfParsedNamedQuery>(request);

            if (namedQueryResult.ParsedQuery is null or { IsFaulty: true }) return null;

            var pdfControlFile =
                await storedNamedQueryService.GetControlFile(namedQueryResult.ParsedQuery.ControlFileStorageKey);
            return pdfControlFile;
        }
    }
}