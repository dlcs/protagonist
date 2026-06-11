using DLCS.Core;
using DLCS.Model.Spaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace API.Features.Space.Requests;

/// <summary>
/// Deletes a specified space for customer
/// </summary>
public class DeleteSpace(int customerId, int spaceId) : IRequest<ResultMessage<DeleteResult>>
{
    public int CustomerId { get; } = customerId;
    public int SpaceId { get; } = spaceId;
}

public class DeleteSpaceHandler(
    ISpaceRepository spaceRepository,
    ILogger<DeleteSpaceHandler> logger)
    : IRequestHandler<DeleteSpace, ResultMessage<DeleteResult>>
{
    public async Task<ResultMessage<DeleteResult>> Handle(DeleteSpace request, CancellationToken cancellationToken)
    {
        if (request.SpaceId == 0)
        {
            return new ResultMessage<DeleteResult>("Space 0 cannot be deleted", DeleteResult.Conflict);
        }

        logger.LogDebug("Deleting Space {SpaceId}", request.SpaceId);
        var deleteResult = await spaceRepository.DeleteSpace(request.CustomerId, request.SpaceId, cancellationToken);

        return deleteResult;
    }
}
