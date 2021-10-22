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
            var space = await dbContext.Spaces.AsNoTracking().SingleOrDefaultAsync(s =>
                s.Customer == request.CustomerId && s.Id == request.SpaceId, cancellationToken: cancellationToken);
            var counter = await dbContext.EntityCounters.AsNoTracking().SingleOrDefaultAsync(ec =>
                ec.Customer == request.CustomerId && ec.Type == "space-images" &&
                ec.Scope == request.SpaceId.ToString(), cancellationToken: cancellationToken);
            if (space != null && counter != null)
            {
                space.ApproximateNumberOfImages = counter.Next;
            }

            return space;
        }
    }
}