using API.Infrastructure.Requests;
using DLCS.Model.Policies;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.Policies.Requests;

/// <summary>
/// Get details of specified origin strategy
/// </summary>
public class GetOriginStrategy : IRequest<FetchEntityResult<OriginStrategy>>
{
    public string OriginStrategyId { get; }

    public GetOriginStrategy(string originStrategyId)
    {
        OriginStrategyId = originStrategyId;
    }
}

public class GetOriginStrategyHandler : IRequestHandler<GetOriginStrategy,
    FetchEntityResult<OriginStrategy>>
{
    private readonly DlcsContext dlcsContext;

    public GetOriginStrategyHandler(DlcsContext dlcsContext)
    {
        this.dlcsContext = dlcsContext;
    }

    public async Task<FetchEntityResult<OriginStrategy>> Handle(GetOriginStrategy request,
        CancellationToken cancellationToken)
    {
        var strategy = await dlcsContext.OriginStrategies.AsNoTracking()
            .SingleOrDefaultAsync(b => b.Id == request.OriginStrategyId,
                cancellationToken);
        return strategy == null
            ? FetchEntityResult<OriginStrategy>.NotFound()
            : FetchEntityResult<OriginStrategy>.Success(strategy);
    }
}