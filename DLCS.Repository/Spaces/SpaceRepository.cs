using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Spaces;
using Microsoft.EntityFrameworkCore;

namespace DLCS.Repository.Spaces
{
    public class SpaceRepository : ISpaceRepository
    {
        private readonly DlcsContext dlcsContext;

        public SpaceRepository(DlcsContext dlcsContext)
        {
            this.dlcsContext = dlcsContext;
        }
        
        public async Task<int?> GetImageCountForSpace(int customerId, int spaceId)
        {
            // NOTE - this is sub-optimal but EntityCounters are not reliable when using PUT
            var count = await dlcsContext.Images.Where(c => c.Customer == customerId && c.Space == spaceId)
                .CountAsync();
            return count;

            /*var entity = await dlcsContext.EntityCounters.AsNoTracking()
                .SingleOrDefaultAsync(ec => ec.Type == "space-images"
                                            && ec.Customer == customerId
                                            && ec.Scope == spaceId.ToString());

            return entity == null ? null : (int) entity.Next;*/
        }

        public Task<Space?> GetSpace(int customerId, int spaceId, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public Task<Space?> GetSpace(int customerId, string name, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public Task<Space> CreateSpace(int customer, string name, string? imageBucket, string[]? tags, string[]? roles, int? maxUnauthorised,
            CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public Task<PageOfSpaces> GetPageOfSpaces(int customerId, int page, int pageSize, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public Task<Space> PatchSpace(int customerId, int spaceId, string? name, int? maxUnauthorised, string[]? tags, string[]? roles,
            CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }
    }
}