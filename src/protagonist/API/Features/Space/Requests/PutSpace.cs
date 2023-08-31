using System.Collections.Generic;
using DLCS.Core.Strings;
using DLCS.Model.Spaces;
using MediatR;

namespace API.Features.Space.Requests;

/// <summary>
/// Create or update an existing space
/// </summary>
public class PutSpace : IRequest<PutSpaceResult>
{
    public int CustomerId { get; set; }
    public int SpaceId { get; set; }
    public string? Name { get; set; }
    public string? ImageBucket { get; set; } = string.Empty;
    public string[]? Tags { get; set; }
    public string[]? Roles { get; set; }
    public int? MaxUnauthorised { get; set; }
}

public class PutSpaceResult
{
    public DLCS.Model.Spaces.Space? Space { get; set; }
    public List<string> ErrorMessages { get; } = new();
    public bool Conflict { get; set; }
}

public class PutSpaceHandler : IRequestHandler<PutSpace, PutSpaceResult>
{
    private readonly ISpaceRepository spaceRepository;

    public PutSpaceHandler(ISpaceRepository spaceRepository)
    {
        this.spaceRepository = spaceRepository;
    }
    
    public async Task<PutSpaceResult> Handle(PutSpace request, CancellationToken cancellationToken)
    {
        var result = new PutSpaceResult();
        
        var sameIdSpace = await spaceRepository.GetSpace(request.CustomerId, request.SpaceId, cancellationToken);
        if (sameIdSpace == null && !request.Name.HasText())
        {
            result.ErrorMessages.Add("A name is required when creating a new space.");
            return result;
        }
        
        var sameNameSpace = await spaceRepository.GetSpace(request.CustomerId, request.Name, cancellationToken);
        if (sameNameSpace != null && sameNameSpace.Id != request.SpaceId)
        {
            result.Conflict = true;
            result.ErrorMessages.Add($"The space name '{request.Name}' is already taken.");
            return result;
        }     
        
        var putSpace = await spaceRepository.PutSpace(
            request.CustomerId, request.SpaceId, request.Name, request.ImageBucket,
            request.MaxUnauthorised, request.Tags, request.Roles,
            cancellationToken);
        
        result.Space = putSpace;
        
        return result;
    }
}