using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Portal.Legacy;
using Portal.Features.Spaces.Models;

namespace Portal.Features.Spaces.Requests
{
    /// <summary>
    /// Request to get details of space from API.
    /// </summary>
    public class GetSpaceDetails : IRequest<SpacePageModel>
    {
        public int SpaceId { get; set; }
    }

    public class GetSpaceDetailsHandler : IRequestHandler<GetSpaceDetails, SpacePageModel>
    {
        private readonly DlcsClient dlcsClient;

        public GetSpaceDetailsHandler(DlcsClient dlcsClient)
        {
            this.dlcsClient = dlcsClient;
        }
        
        public async Task<SpacePageModel> Handle(GetSpaceDetails request, CancellationToken cancellationToken)
        {
            return new SpacePageModel
            {
                Space = await dlcsClient.GetSpaceDetails(request.SpaceId),
                Images = await dlcsClient.GetSpaceImages(request.SpaceId)
            };
        }
    }
}