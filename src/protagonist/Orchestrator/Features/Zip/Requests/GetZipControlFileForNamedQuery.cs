using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Assets.NamedQueries;
using MediatR;
using Orchestrator.Infrastructure.NamedQueries.Persistence;
using Orchestrator.Infrastructure.NamedQueries.Persistence.Models;
using Orchestrator.Infrastructure.NamedQueries.Requests;

namespace Orchestrator.Features.Zip.Requests
{
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
        private readonly StoredNamedQueryService storedNamedQueryService;
        private readonly NamedQueryResultGenerator namedQueryResultGenerator;

        public GetZipControlFileForNamedQueryHandler(
            StoredNamedQueryService storedNamedQueryService,
            NamedQueryResultGenerator namedQueryResultGenerator)
        {
            this.storedNamedQueryService = storedNamedQueryService;
            this.namedQueryResultGenerator = namedQueryResultGenerator;
        }
        
        public async Task<ControlFile?> Handle(GetZipControlFileForNamedQuery request, CancellationToken cancellationToken)
        {
            var namedQueryResult = await namedQueryResultGenerator.GetNamedQueryResult<ZipParsedNamedQuery>(request);

            if (namedQueryResult.ParsedQuery is null or { IsFaulty: true }) return null;

            var controlFile =
                await storedNamedQueryService.GetControlFile(namedQueryResult.ParsedQuery.ControlFileStorageKey,
                    cancellationToken);
            return controlFile;
        }
    }
}