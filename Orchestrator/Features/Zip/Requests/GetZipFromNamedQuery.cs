using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Assets.NamedQueries;
using MediatR;
using Orchestrator.Features.PDF;
using Orchestrator.Infrastructure.NamedQueries.Persistence;
using Orchestrator.Infrastructure.NamedQueries.Persistence.Models;
using Orchestrator.Infrastructure.NamedQueries.Requests;

namespace Orchestrator.Features.Zip.Requests
{
    public class GetZipFromNamedQuery : IBaseNamedQueryRequest, IRequest<PersistedNamedQueryProjection>
    {
        public string CustomerPathValue { get; }
        public string NamedQuery { get; }
        public string? NamedQueryArgs { get; }
        
        public GetZipFromNamedQuery(string customerPathValue, string namedQuery, string? namedQueryArgs)
        {
            CustomerPathValue = customerPathValue;
            NamedQuery = namedQuery;
            NamedQueryArgs = namedQueryArgs;
        }
    }
    
    public class GetZipFromNamedQueryHandler : IRequestHandler<GetZipFromNamedQuery, PersistedNamedQueryProjection>
    {
        private readonly StoredNamedQueryService storedNamedQueryService;
        private readonly NamedQueryResultGenerator namedQueryResultGenerator;
        private readonly IProjectionCreator<ZipParsedNamedQuery> zipCreator;

        public GetZipFromNamedQueryHandler(
            StoredNamedQueryService storedNamedQueryService,
            NamedQueryResultGenerator namedQueryResultGenerator,
            IProjectionCreator<ZipParsedNamedQuery> zipCreator)
        {
            this.storedNamedQueryService = storedNamedQueryService;
            this.namedQueryResultGenerator = namedQueryResultGenerator;
            this.zipCreator = zipCreator;
        }
        
        public async Task<PersistedNamedQueryProjection> Handle(GetZipFromNamedQuery request, CancellationToken cancellationToken)
        {
            var namedQueryResult =
                await namedQueryResultGenerator.GetNamedQueryResult<ZipParsedNamedQuery>(request);

            if (namedQueryResult.ParsedQuery == null)
                return new PersistedNamedQueryProjection(PersistedProjectionStatus.NotFound);
            if (namedQueryResult.ParsedQuery is { IsFaulty: true })
                return PersistedNamedQueryProjection.BadRequest();
            
            // Stream ZIP 
            var zipResult =
                await storedNamedQueryService.GetResults(namedQueryResult, zipCreator, false, cancellationToken);

            return zipResult.Status == PersistedProjectionStatus.InProcess
                ? new PersistedNamedQueryProjection(PersistedProjectionStatus.InProcess)
                : new PersistedNamedQueryProjection(zipResult.Stream, zipResult.Status);
        }
    }
}