using System.Collections.Generic;
using System.Net;
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
    public int CustomerId { get; set; }
    public int SpaceId { get; set; }
    public string? Name { get; set; }
    public string[]? Tags { get; set; }
    public string[]? Roles { get; set; }
    public int? MaxUnauthorised { get; set; }
}

public class PatchSpaceHandler : IRequestHandler<PatchSpace, ModifyEntityResult<DLCS.Model.Spaces.Space>>
{
    private readonly ISpaceRepository spaceRepository;

    public PatchSpaceHandler(ISpaceRepository spaceRepository)
    {
        this.spaceRepository = spaceRepository;
    }
    
    public async Task<ModifyEntityResult<DLCS.Model.Spaces.Space>> Handle(PatchSpace request, CancellationToken cancellationToken)
    {
        var sameIdSpace = await spaceRepository.GetSpace(request.CustomerId, request.SpaceId, cancellationToken);
        if (sameIdSpace == null)
            return ModifyEntityResult<DLCS.Model.Spaces.Space>.Failure($"Couldn't find a space with the id {request.SpaceId}", WriteResult.NotFound);
   
        if (request.Name.HasText())
        {
            var sameNameSpace = await spaceRepository.GetSpace(request.CustomerId, request.Name, cancellationToken);
            if (sameNameSpace != null && sameNameSpace.Id != request.SpaceId)
                return ModifyEntityResult<DLCS.Model.Spaces.Space>.Failure($"The space name '{request.Name}' is already taken.", WriteResult.Conflict);
        }
        
        // The request customer and space override any values for these that may
        // (or more likely, may not) have been sent on the incoming Space to be patched.
        var patchedSpace = await spaceRepository.PatchSpace(
            request.CustomerId, request.SpaceId, request.Name,
            request.MaxUnauthorised, request.Tags, request.Roles,
            cancellationToken);
        
        return ModifyEntityResult<DLCS.Model.Spaces.Space>.Success(patchedSpace);
    }
}