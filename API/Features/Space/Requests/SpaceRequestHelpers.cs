using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Repository;
using Microsoft.EntityFrameworkCore;

namespace API.Features.Space.Requests
{
    public class SpaceRequestHelpers
    {
        public static async Task<DLCS.Repository.Entities.Space?> GetSpaceInternal(
            DlcsContext dbContext, int customerId, int spaceId, CancellationToken cancellationToken)
        {
            var space = await dbContext.Spaces.AsNoTracking().SingleOrDefaultAsync(s =>
                s.Customer == customerId && s.Id == spaceId, cancellationToken: cancellationToken);
            var counter = await dbContext.EntityCounters.AsNoTracking().SingleOrDefaultAsync(ec =>
                ec.Customer == customerId && ec.Type == "space-images" &&
                ec.Scope == spaceId.ToString(), cancellationToken: cancellationToken);
            if (space != null && counter != null)
            {
                space.ApproximateNumberOfImages = counter.Next;
            }

            return space;
        }
        
        
        public static async Task EnsureSpaceNameNotTaken(
            DlcsContext dbContext, int customerId, string name, CancellationToken cancellationToken)
        {
            var existing = await dbContext.Spaces
                .Where(s => s.Customer == customerId)
                .SingleOrDefaultAsync(s => s.Name == name, cancellationToken: cancellationToken);
            if (existing != null)
            {
                throw new BadRequestException("A space with this name already exists for this customer.");
            }
        }
    }
}