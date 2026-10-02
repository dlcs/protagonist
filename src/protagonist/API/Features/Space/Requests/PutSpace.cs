using API.Infrastructure.Requests;
using DLCS.Core;
using DLCS.Core.Strings;
using DLCS.Model.Spaces;
using MediatR;

namespace API.Features.Space.Requests;

/// <summary>
/// Create or update an existing space
/// </summary>
public class PutSpace : IRequest<ModifyEntityResult<DLCS.Model.Spaces.Space>>
{
    public int CustomerId { get; init; }
    public int SpaceId { get; init; }
    public string? Name { get; init; }
    public string[]? Tags { get; init; }
    public string[]? Roles { get; init; }
}

public class PutSpaceHandler(ISpaceRepository spaceRepository)
    : IRequestHandler<PutSpace, ModifyEntityResult<DLCS.Model.Spaces.Space>>
{
    public async Task<ModifyEntityResult<DLCS.Model.Spaces.Space>> Handle(PutSpace request, CancellationToken cancellationToken)
    {
        if (request.SpaceId <= 0)
        {
            return ModifyEntityResult<DLCS.Model.Spaces.Space>.Failure("Space id must be a positive integer",
                WriteResult.FailedValidation);
        }

        var sameIdSpace = await spaceRepository.GetSpace(request.CustomerId, request.SpaceId, cancellationToken);
        if (sameIdSpace == null && !request.Name.HasText())
        {
            return ModifyEntityResult<DLCS.Model.Spaces.Space>.Failure("A name is required when creating a new space.",
                WriteResult.FailedValidation);            
        }
        
        if (request.Name.HasText())
        {
            var sameNameSpace = await spaceRepository.GetSpace(request.CustomerId, request.Name, cancellationToken);
            if (sameNameSpace != null && sameNameSpace.Id != request.SpaceId)
            {
                return ModifyEntityResult<DLCS.Model.Spaces.Space>.Failure(
                    $"The space name '{request.Name}' is already taken.", WriteResult.Conflict);
            }
        }

        var putSpaceResult = await spaceRepository.UpsertSpace(request.CustomerId, request.SpaceId, request.Name,
            request.Tags, request.Roles, cancellationToken);
        
        var result = sameIdSpace == null ? WriteResult.Created : WriteResult.Updated;
        
        return ModifyEntityResult<DLCS.Model.Spaces.Space>.Success(putSpaceResult, result);
    }
}
