using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Strings;
using DLCS.Model.Spaces;
using DLCS.Repository;
using MediatR;

namespace API.Features.Space.Requests
{
    /// <summary>
    /// 
    /// </summary>
    public class PatchSpace : IRequest<DLCS.Model.Spaces.Space>
    {
        public int CustomerId { get; set; }
        public int SpaceId { get; set; }
        public string? Name { get; set; }
        public string[]? Tags { get; set; }
        public string[]? Roles { get; set; }
        public int? MaxUnauthorised { get; set; }
    }

    public class PatchSpaceHandler : IRequestHandler<PatchSpace, DLCS.Model.Spaces.Space>
    {
        private readonly ISpaceRepository spaceRepository;

        public PatchSpaceHandler(ISpaceRepository spaceRepository)
        {
            this.spaceRepository = spaceRepository;
        }
        
        public async Task<DLCS.Model.Spaces.Space> Handle(PatchSpace request, CancellationToken cancellationToken)
        {
            if (request.Name.HasText())
            {
                var existing = await spaceRepository.GetSpace(request.CustomerId, request.Name, cancellationToken);
                if (existing != null)
                {
                    throw new BadRequestException($"The space name '{request.Name}' is already taken.");
                }     
            }
            // The request customer and space override any values for these that may
            // (or more likely, may not) have been sent on the incoming Space to be patched.
            var patchedSpace = await spaceRepository.PatchSpace(
                request.CustomerId, request.SpaceId, request.Name,
                request.MaxUnauthorised, request.Tags, request.Roles,
                cancellationToken);
            return patchedSpace;
        }
    }
}