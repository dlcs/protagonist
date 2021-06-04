using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace DLCS.Repository.Spaces
{
    public interface ISpaceRepository
    {
        Task<int?> GetImageCountForSpace(int customerId, int spaceId);
    }

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
    }
}