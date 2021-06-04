using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading;
using System.Threading.Tasks;
using API.JsonLd;
using MediatR;
using Microsoft.Extensions.Logging;
using Portal.Legacy;
using Portal.Features.Spaces.Models;

namespace Portal.Features.Spaces.Requests
{
    /// <summary>
    /// Request to get details of space from API.
    /// </summary>
    public class GetSpaceDetails : IRequest<SpacePageModel>
    {
        public int SpaceId { get; }

        // TODO - would this be better strongly typed?
        public string? ImageOrderBy { get; }

        /// <summary>
        /// Constructor for GetSpaceDetails request.
        /// </summary>
        /// <param name="spaceId">Id of space to get details for.</param>
        /// <param name="imageOrderBy">Optional orderBy clause for space images</param>
        public GetSpaceDetails(int spaceId, string? imageOrderBy = null)
        {
            SpaceId = spaceId;
            ImageOrderBy = imageOrderBy;
        }
    }

    public class GetSpaceDetailsHandler : IRequestHandler<GetSpaceDetails, SpacePageModel>
    {
        private readonly DlcsClient dlcsClient;
        private readonly ILogger<GetSpaceDetailsHandler> logger;

        public GetSpaceDetailsHandler(DlcsClient dlcsClient, ILogger<GetSpaceDetailsHandler> logger)
        {
            this.dlcsClient = dlcsClient;
            this.logger = logger;
        }
        
        public async Task<SpacePageModel> Handle(GetSpaceDetails request, CancellationToken cancellationToken)
        {
            var images = await GetSpaceImages(request);
            return new SpacePageModel
            {
                Space = await dlcsClient.GetSpaceDetails(request.SpaceId),
                Images = images
            };
        }

        private async Task<HydraImageCollection?> GetSpaceImages(GetSpaceDetails request)
        {
            var images = await dlcsClient.GetSpaceImages(request.SpaceId);

            if (!string.IsNullOrWhiteSpace(request.ImageOrderBy))
            {
                logger.LogDebug("Ordering space queries by '{orderBy}'", request.ImageOrderBy);
                images.Members = images.Members.AsQueryable().OrderBy(request.ImageOrderBy).ToArray();
            }

            return images;
        }
    }
}