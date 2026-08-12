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
    public string? ImageBucket { get; set; }
    public string[]? Tags { get; set; }
    public string[]? Roles { get; set; }
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
            request.ImageBucket, request.Tags, request.Roles, cancellationToken);
        
        var result = sameIdSpace == null ? WriteResult.Created : WriteResult.Updated;
        
        return ModifyEntityResult<DLCS.Model.Spaces.Space>.Success(putSpaceResult, result);
    }
}
