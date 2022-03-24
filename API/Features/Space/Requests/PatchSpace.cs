using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Strings;
using DLCS.Repository;
using MediatR;

namespace API.Features.Space.Requests
{
    /// <summary>
    /// 
    /// </summary>
    public class PatchSpace : IRequest<DLCS.Repository.Entities.Space>
    {
        public DLCS.HydraModel.Space HydraSpace { get; set; }
        public int CustomerId { get; set; }
        public int SpaceId { get; set; }
        
        public PatchSpace(int customerId, int spaceId, DLCS.HydraModel.Space hydraSpace)
        {
            CustomerId = customerId;
            SpaceId = spaceId;
            HydraSpace = hydraSpace;
        }
    }

    public class PatchSpaceHandler : IRequestHandler<PatchSpace, DLCS.Repository.Entities.Space>
    {
        private readonly DlcsContext dbContext;

        public PatchSpaceHandler(DlcsContext dlcsContext)
        {
            this.dbContext = dlcsContext;
        }
        
        public async Task<DLCS.Repository.Entities.Space> Handle(PatchSpace request, CancellationToken cancellationToken)
        {
            // The request customer and space override any values for these that may
            // (or more likely, may not) have been sent on the incoming Space to be patched.
            var keys = new object[] {request.SpaceId, request.CustomerId}; // Keys are in this order
            var dbSpace = await dbContext.Spaces.FindAsync(keys, cancellationToken);
            var hydraSpace = request.HydraSpace;
            if (hydraSpace.Name.HasText() && hydraSpace.Name != dbSpace.Name)
            {
                await SpaceRequestHelpers.EnsureSpaceNameNotTaken(
                    dbContext, request.CustomerId, hydraSpace.Name, cancellationToken);
                dbSpace.Name = hydraSpace.Name;
            }

            if (hydraSpace.DefaultTags != null)
            {
                dbSpace.Tags = hydraSpace.DefaultTags;
            }

            if (hydraSpace.DefaultRoles != null)
            {
                dbSpace.Roles = hydraSpace.DefaultRoles;
            }

            if (hydraSpace.MaxUnauthorised != null)
            {
                dbSpace.MaxUnauthorised = hydraSpace.MaxUnauthorised ?? -1;
            }

            // ImageBucket?
            
            await dbContext.SaveChangesAsync(cancellationToken);
            
            var retrievedSpace = await SpaceRequestHelpers.GetSpaceInternal(
                dbContext, request.CustomerId, request.SpaceId, cancellationToken);
            return retrievedSpace;
        }
    }
}