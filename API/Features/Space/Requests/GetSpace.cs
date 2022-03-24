using System.Threading;
using System.Threading.Tasks;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.Space.Requests
{
    public class GetSpace : IRequest<DLCS.Repository.Entities.Space?>
    {
        public GetSpace(int customerId, int spaceId)
        {
            CustomerId = customerId;
            SpaceId = spaceId;
        }
        
        public int CustomerId { get; private set; }
        public int SpaceId { get; private set; }
    }

    public class GetSpaceHandler : IRequestHandler<GetSpace, DLCS.Repository.Entities.Space?>
    {
        private readonly DlcsContext dbContext;

        public GetSpaceHandler(DlcsContext dlcsContext)
        {
            this.dbContext = dlcsContext;
        }
        
        public async Task<DLCS.Repository.Entities.Space?> Handle(GetSpace request, CancellationToken cancellationToken)
        {
            var space = await SpaceRequestHelpers.GetSpaceInternal(
                dbContext, request.CustomerId, request.SpaceId, cancellationToken);
            return space;
        }
    }
}