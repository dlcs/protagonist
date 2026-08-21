using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core;
using DLCS.Core.Types;
using DLCS.Model.Assets.NamedQueries;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Orchestrator.Infrastructure.NamedQueries;
using Orchestrator.Infrastructure.NamedQueries.Requests;

namespace Orchestrator.Features.Query.Requests;

/// <summary>
/// Get asset-id of every asset matching named query 
/// </summary>
public class GetNamedQueryAssetIds(string customerPathValue, string namedQuery, string? namedQueryArgs)
    : IBaseNamedQueryRequest, IRequest<ResultStatus<IEnumerable<AssetId>>>
{
    public string CustomerPathValue { get; } = customerPathValue;

    public string NamedQuery { get; } = namedQuery;

    public string? NamedQueryArgs { get; } = namedQueryArgs;
}

public class GetNamedQueryResultHandler(NamedQueryResultGenerator namedQueryResultGenerator)
    : IRequestHandler<GetNamedQueryAssetIds, ResultStatus<IEnumerable<AssetId>>>
{
    public async Task<ResultStatus<IEnumerable<AssetId>>> Handle(GetNamedQueryAssetIds request, CancellationToken cancellationToken)
    {
        try
        {
            var resultContainer = await namedQueryResultGenerator.GetNamedQueryResult<ParsedNamedQuery>(request);
            var namedQueryResult = resultContainer.NamedQueryResult;

            if (namedQueryResult.ParsedQuery == null)
                return ResultStatus<IEnumerable<AssetId>>.Unsuccessful([]);
            if (namedQueryResult.ParsedQuery is { IsFaulty: true })
                return ResultStatus<IEnumerable<AssetId>>.Unsuccessful([], 400);

            var matchingAssetIds = await namedQueryResult.Results
                .OrderByNamedQuery(namedQueryResult.ParsedQuery)
                .Select(a => a.Id)
                .ToListAsync(cancellationToken);
            return ResultStatus<IEnumerable<AssetId>>.Successful(matchingAssetIds);
        }
        catch (KeyNotFoundException)
        {
            return ResultStatus<IEnumerable<AssetId>>.Unsuccessful(Enumerable.Empty<AssetId>(), 404);
        }
    }
}
