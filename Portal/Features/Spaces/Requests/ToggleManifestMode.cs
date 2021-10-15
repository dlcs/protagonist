using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using API.Client;
using DLCS.Repository;
using DLCS.Repository.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Portal.Behaviours;
using SpaceX = API.Client.OldJsonLd.SpaceX;

namespace Portal.Features.Spaces.Requests
{
    /// <summary>
    /// Convert space to normal/manifest mode
    /// </summary>
    public class ToggleManifestMode : IRequest<bool>, IAuditable
    {
        public int SpaceId { get; }
        public bool ToggleOn { get; }

        public ToggleManifestMode(int spaceId, bool toggleOn)
        {
            SpaceId = spaceId;
            ToggleOn = toggleOn;
        }
    }
    
    public class ToggleManifestModeHandler : IRequestHandler<ToggleManifestMode, bool>
    {
        private readonly DlcsContext dbContext;
        private readonly ClaimsPrincipal claimsPrincipal;

        public ToggleManifestModeHandler(
            DlcsContext dbContext,
            ClaimsPrincipal claimsPrincipal)
        {
            this.dbContext = dbContext;
            this.claimsPrincipal = claimsPrincipal;
        }
        
        public async Task<bool> Handle(ToggleManifestMode request, CancellationToken cancellationToken)
        {
            // NOTE: Updating Tags is not supported in API so do directly into DB.
            // Space tag management should go into a space repository/service rather than isolated here as it will be reusable.
            var dbSpace = await GetSpace(request.SpaceId, claimsPrincipal.GetCustomerId() ?? 0, cancellationToken);

            return await ToggleManifestMode(request.ToggleOn, dbSpace, cancellationToken);
        }

        private Task<Space> GetSpace(int spaceId, int customerId, CancellationToken cancellationToken)
            => dbContext.Spaces.SingleAsync(s => s.Id == spaceId && s.Customer == customerId,
                cancellationToken: cancellationToken);
        
        private async Task<bool> ToggleManifestMode(bool toggleOn, Space? dbSpace, CancellationToken cancellationToken)
        {
            if (toggleOn)
            {
                dbSpace.AddTag(SpaceX.ManifestTag);
            }
            else
            {
                dbSpace.RemoveTag(SpaceX.ManifestTag);
            }

            var changeCount = await dbContext.SaveChangesAsync(cancellationToken);
            return changeCount > 0;
        }
    }
}