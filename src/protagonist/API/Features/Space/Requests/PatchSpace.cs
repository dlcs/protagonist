using System.Collections.Generic;
using DLCS.Core.Strings;
using DLCS.Model.Spaces;
using MediatR;

namespace API.Features.Space.Requests;

/// <summary>
/// Make a partial updated to an existing space
/// </summary>
public class PatchSpace : IRequest<PatchSpaceResult>
{
    public int CustomerId { get; set; }
    public int SpaceId { get; set; }
    public string? Name { get; set; }
    public string[]? Tags { get; set; }
    public string[]? Roles { get; set; }
    public int? MaxUnauthorised { get; set; }
}

public class PatchSpaceResult
{
    public DLCS.Model.Spaces.Space? Space { get; set; }
    public List<string> ErrorMessages { get; } = new();
    public bool Conflict { get; set; }
}

public class PatchSpaceHandler : IRequestHandler<PatchSpace, PatchSpaceResult>
{
    private readonly ISpaceRepository spaceRepository;

    public PatchSpaceHandler(ISpaceRepository spaceRepository)
    {
        this.spaceRepository = spaceRepository;
    }
    
    public async Task<PatchSpaceResult> Handle(PatchSpace request, CancellationToken cancellationToken)
    {
        var result = new PatchSpaceResult();
        if (request.Name.HasText())
        {
            var existing = await spaceRepository.GetSpace(request.CustomerId, request.Name, cancellationToken);
            if (existing != null && existing.Id != request.SpaceId)
            {
                result.Conflict = true;
                result.ErrorMessages.Add($"The space name '{request.Name}' is already taken.");
                return result;
            }     
        }
        // The request customer and space override any values for these that may
        // (or more likely, may not) have been sent on the incoming Space to be patched.
        var patchedSpace = await spaceRepository.PatchSpace(
            request.CustomerId, request.SpaceId, request.Name,
            request.MaxUnauthorised, request.Tags, request.Roles,
            cancellationToken);
        result.Space = patchedSpace;
        return result;
    }
}