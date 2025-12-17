using DLCS.Core;
using DLCS.Core.Types;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.Adjuncts.Requests;

public class DeleteAdjunct(string id, AssetId assetId) : IRequest<ResultMessage<DeleteResult>>
{
    public string Id { get; } = id;
    
    public AssetId AssetId { get; } = assetId;
}


public class DeleteAdjunctHandler(DlcsContext dbContext)
    : IRequestHandler<DeleteAdjunct, ResultMessage<DeleteResult>>
{
    public async Task<ResultMessage<DeleteResult>> Handle(DeleteAdjunct request, CancellationToken cancellationToken)
    {
        var deletion = await dbContext
            .Adjuncts
            .Where(a => a.Id == request.Id && a.AssetId == request.AssetId)
            .ExecuteDeleteAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (deletion == 0)
        {
            return new ResultMessage<DeleteResult>($"Couldn't find an adjunct with the id {request.Id}",
                DeleteResult.NotFound);
        }

        return new ResultMessage<DeleteResult>(string.Empty, DeleteResult.Deleted);
    }
}
