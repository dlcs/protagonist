using System.Collections.Generic;
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
    public int CustomerId { get; set; }
    public int SpaceId { get; set; }
    public string? Name { get; set; }
    public string? ImageBucket { get; set; } = string.Empty;
    public string[]? Tags { get; set; }
    public string[]? Roles { get; set; }
    public int? MaxUnauthorised { get; set; }
}

public class PutSpaceHandler : IRequestHandler<PutSpace, ModifyEntityResult<DLCS.Model.Spaces.Space>>
{
    private readonly ISpaceRepository spaceRepository;

    public PutSpaceHandler(ISpaceRepository spaceRepository)
    {
        this.spaceRepository = spaceRepository;
    }
    
    public async Task<ModifyEntityResult<DLCS.Model.Spaces.Space>> Handle(PutSpace request, CancellationToken cancellationToken)
    {
        var sameIdSpace = await spaceRepository.GetSpace(request.CustomerId, request.SpaceId, cancellationToken);
        if (sameIdSpace == null && !request.Name.HasText())
            return ModifyEntityResult<DLCS.Model.Spaces.Space>.Failure("A name is required when creating a new space.",
                WriteResult.FailedValidation);

        if (request.Name.HasText())
        {
            var sameNameSpace = await spaceRepository.GetSpace(request.CustomerId, request.Name, cancellationToken);
            if (sameNameSpace != null && sameNameSpace.Id != request.SpaceId)
                return ModifyEntityResult<DLCS.Model.Spaces.Space>.Failure($"The space name '{request.Name}' is already taken.",
                    WriteResult.Conflict);
        }
        
        var putSpaceResult = await spaceRepository.PutSpace(
            request.CustomerId, request.SpaceId, request.Name, request.ImageBucket,
            request.MaxUnauthorised, request.Tags, request.Roles,
            cancellationToken);
       
        return ModifyEntityResult<DLCS.Model.Spaces.Space>.Success(putSpaceResult);
    }
}