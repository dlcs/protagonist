using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Spaces;
using MediatR;

namespace API.Features.Space.Requests;

public class GetSpace : IRequest<DLCS.Model.Spaces.Space?>
{
    public GetSpace(int customerId, int spaceId)
    {
        CustomerId = customerId;
        SpaceId = spaceId;
    }
    
    public int CustomerId { get; private set; }
    public int SpaceId { get; private set; }
}

public class GetSpaceHandler : IRequestHandler<GetSpace, DLCS.Model.Spaces.Space?>
{
    private readonly ISpaceRepository spaceRepository;

    public GetSpaceHandler(ISpaceRepository spaceRepository)
    {
        this.spaceRepository = spaceRepository;
    }
    
    public async Task<DLCS.Model.Spaces.Space?> Handle(GetSpace request, CancellationToken cancellationToken)
    {
        var space = await spaceRepository.GetSpace(
            request.CustomerId, request.SpaceId, cancellationToken, noCache:true);
        return space;
    }
}