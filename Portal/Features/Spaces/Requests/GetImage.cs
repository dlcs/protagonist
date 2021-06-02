using System.Threading;
using System.Threading.Tasks;
using API.JsonLd;
using MediatR;
using Portal.Features.Spaces.Models;
using Portal.Legacy;

namespace Portal.Features.Spaces.Requests
{
    public class GetImage : IRequest<Image>
    {
        public int SpaceId { get; set; }
        public string ImageId { get; set; }
    }
    
    public class GetImageHandler : IRequestHandler<GetImage, Image>
    {
        private readonly DlcsClient dlcsClient;

        public GetImageHandler(DlcsClient dlcsClient)
        {
            this.dlcsClient = dlcsClient;
        }
        
        public async Task<Image> Handle(GetImage request, CancellationToken cancellationToken)
        {
            return await dlcsClient.GetImage(request.SpaceId, request.ImageId);
        }
    }
}