using API.Infrastructure.Requests;
using DLCS.Core;
using DLCS.Core.Strings;
using DLCS.Model.Spaces;
using MediatR;

namespace API.Features.Space.Requests;

/// <summary>
/// Make a partial updated to an existing space
/// </summary>
public class PatchSpace : IRequest<ModifyEntityResult<DLCS.Model.Spaces.Space>>
{
    public int CustomerId { get; init; }
    public int SpaceId { get; init; }
    public string? Name { get; init; }
    public string[]? Tags { get; init; }
    public string[]? Roles { get; init; }
}

public class PatchSpaceHandler(ISpaceRepository spaceRepository)
    : IRequestHandler<PatchSpace, ModifyEntityResult<DLCS.Model.Spaces.Space>>
{
    public async Task<ModifyEntityResult<DLCS.Model.Spaces.Space>> Handle(PatchSpace request, CancellationToken cancellationToken)
    {
        if (request.SpaceId <= 0)
        {
            return ModifyEntityResult<DLCS.Model.Spaces.Space>.Failure(
                "Space id must be a positive integer",
                WriteResult.FailedValidation);
        }

        var sameIdSpace = await spaceRepository.GetSpace(request.CustomerId, request.SpaceId, cancellationToken);
        if (sameIdSpace == null)
        {
            return ModifyEntityResult<DLCS.Model.Spaces.Space>.Failure(
                $"Couldn't find a space with the id {request.SpaceId}", WriteResult.NotFound);
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
        
        // The request customer and space override any values for these that may
        // (or more likely, may not) have been sent on the incoming Space to be patched.
        var patchedSpace = await spaceRepository.PatchSpace(request.CustomerId, request.SpaceId, request.Name,
            request.Tags, request.Roles, cancellationToken);
        
        return ModifyEntityResult<DLCS.Model.Spaces.Space>.Success(patchedSpace);
    }
}
