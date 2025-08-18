using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core;
using DLCS.Core.Types;
using DLCS.Model.Assets.NamedQueries;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Orchestrator.Infrastructure.NamedQueries.Requests;

namespace Orchestrator.Features.Query.Requests;

/// <summary>
/// Get asset-id of every asset matching named query 
/// </summary>
public class GetNamedQueryAssetIds : IBaseNamedQueryRequest, IRequest<ResultStatus<IEnumerable<AssetId>>>
{
    public string CustomerPathValue { get; }
    
    public string NamedQuery { get; }
    
    public string? NamedQueryArgs { get; }
    
    public GetNamedQueryAssetIds(string customerPathValue, string namedQuery, string? namedQueryArgs)
    {
        CustomerPathValue = customerPathValue;
        NamedQuery = namedQuery;
        NamedQueryArgs = namedQueryArgs;
    }
}

public class GetNamedQueryResultHandler : IRequestHandler<GetNamedQueryAssetIds, ResultStatus<IEnumerable<AssetId>>>
{
    private readonly NamedQueryResultGenerator namedQueryResultGenerator;

    public GetNamedQueryResultHandler(NamedQueryResultGenerator namedQueryResultGenerator)
    {
        this.namedQueryResultGenerator = namedQueryResultGenerator;
    }

    public async Task<ResultStatus<IEnumerable<AssetId>>> Handle(GetNamedQueryAssetIds request, CancellationToken cancellationToken)
    {
        try
        {
            var resultContainer = await namedQueryResultGenerator.GetNamedQueryResult<ParsedNamedQuery>(request);
            var namedQueryResult = resultContainer.NamedQueryResult;

            if (namedQueryResult.ParsedQuery == null)
                return ResultStatus<IEnumerable<AssetId>>.Unsuccessful(Enumerable.Empty<AssetId>());
            if (namedQueryResult.ParsedQuery is { IsFaulty: true })
                return ResultStatus<IEnumerable<AssetId>>.Unsuccessful(Enumerable.Empty<AssetId>(), 400);

            var matchingAssetIds = await namedQueryResult.Results.Select(a => a.Id).ToListAsync(cancellationToken);
            return ResultStatus<IEnumerable<AssetId>>.Successful(matchingAssetIds);
        }
        catch (KeyNotFoundException)
        {
            return ResultStatus<IEnumerable<AssetId>>.Unsuccessful(Enumerable.Empty<AssetId>(), 404);
        }
    }
}
