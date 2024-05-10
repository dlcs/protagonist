using DLCS.Core;
using DLCS.Model.Spaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace API.Features.Space.Requests;

/// <summary>
/// Deletes a specified space for customer
/// </summary>
public class DeleteSpace: IRequest<ResultMessage<DeleteResult>>
{
    public DeleteSpace(int customerId, int spaceId)
    {
        CustomerId = customerId;
        SpaceId = spaceId;
    }
    
    public int CustomerId { get; }
    public int SpaceId { get; }
}

public class DeleteSpaceHandler : IRequestHandler<DeleteSpace, ResultMessage<DeleteResult>>
{
    private readonly ISpaceRepository spaceRepository;
    private readonly ILogger<DeleteSpaceHandler> logger;

    public DeleteSpaceHandler(
        ISpaceRepository spaceRepository,
        ILogger<DeleteSpaceHandler> logger)
    {
        this.spaceRepository = spaceRepository;
        this.logger = logger;
    }
    
    public async Task<ResultMessage<DeleteResult>> Handle(DeleteSpace request, CancellationToken cancellationToken)
    {
        logger.LogDebug("Deleting Space {SpaceId}", request.SpaceId);
        var deleteResult = await spaceRepository.DeleteSpace(request.CustomerId, request.SpaceId, cancellationToken);

        return deleteResult;
    }
}