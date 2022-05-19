using System;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using API.Client;
using DLCS.Core;
using DLCS.Core.Settings;
using DLCS.HydraModel;
using DLCS.Web.Auth;
using Hydra.Collections;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Portal.Features.Spaces.Models;
using Portal.Settings;
using Image = DLCS.HydraModel.Image;

namespace Portal.Features.Spaces.Requests
{
    /// <summary>
    /// Request to get details of space from API.
    /// </summary>
    public class GetSpaceDetails : IRequest<SpacePageModel>
    {
        public int SpaceId { get; }
        public int Page { get; set; }
        public int PageSize { get; set; }

        // TODO - would this be better strongly typed?
        public string? ImageOrderBy { get; }

        /// <summary>
        /// Constructor for GetSpaceDetails request.
        /// </summary>
        /// <param name="spaceId">Id of space to get details for.</param>
        /// <param name="imageOrderBy">Optional orderBy clause for space images</param>
        public GetSpaceDetails(int spaceId, int page, int pageSize, string? imageOrderBy = null)
        {
            SpaceId = spaceId;
            Page = page;
            PageSize = pageSize;
            ImageOrderBy = imageOrderBy;
        }
    }

    public class GetSpaceDetailsHandler : IRequestHandler<GetSpaceDetails, SpacePageModel>
    {
        private readonly IDlcsClient dlcsClient;
        private readonly ClaimsPrincipal claimsPrincipal;
        private readonly PortalSettings portalSettings;
        private readonly DlcsSettings dlcsSettings;
        private readonly ILogger<GetSpaceDetailsHandler> logger;

        public GetSpaceDetailsHandler(
            IDlcsClient dlcsClient, 
            IOptions<PortalSettings> portalSettings,
            IOptions<DlcsSettings> dlcsSettings,
            ClaimsPrincipal claimsPrincipal,
            ILogger<GetSpaceDetailsHandler> logger)
        {
            this.dlcsClient = dlcsClient;
            this.claimsPrincipal = claimsPrincipal;
            this.dlcsSettings = dlcsSettings.Value;
            this.portalSettings = portalSettings.Value;
            this.logger = logger;
        }
        
        public async Task<SpacePageModel> Handle(GetSpaceDetails request, CancellationToken cancellationToken)
        {
            var images = await dlcsClient.GetSpaceImages(
                request.Page, request.PageSize, request.SpaceId, request.ImageOrderBy);
            var space = await dlcsClient.GetSpaceDetails(request.SpaceId);
            
            var model = new SpacePageModel
            {
                Space = space,
                Images = images,
                IsManifestSpace = space?.IsManifestSpace() ?? false
            };

            if (model.IsManifestSpace)
            {
                SetManifestLinks(model, request);
            }

            return model;
        }
        
        private void SetManifestLinks(SpacePageModel model, GetSpaceDetails request)
        {
            var namedQuery = DlcsPathHelpers.GeneratePathFromTemplate(
                dlcsSettings.SpaceManifestQuery,
                customer: claimsPrincipal.GetCustomerId().ToString(),
                space: request.SpaceId.ToString());
                
            model.NamedQuery = new Uri(namedQuery);
            model.UniversalViewer = new Uri(string.Concat(portalSettings.UVUrl, "?manifest=", namedQuery));
            model.MiradorViewer = new Uri(string.Concat(portalSettings.MiradorUrl, "?manifest=", namedQuery));
        }
    }
}