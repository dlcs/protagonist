using System.Threading;
using System.Threading.Tasks;
using API.JsonLd;
using MediatR;
using Portal.Legacy;

namespace Portal.Features.Spaces.Requests
{
    public class PatchImages : IRequest<HydraImageCollection>
    {
        public HydraImageCollection Images { get; set; }
        public int SpaceId { get; set; }
    }

    public class PatchImagesHandler : IRequestHandler<PatchImages, HydraImageCollection>
    {
        private readonly DlcsClient dlcsClient;

        public PatchImagesHandler(DlcsClient dlcsClient)
        {
            this.dlcsClient = dlcsClient;
        }
        
        
        public async Task<HydraImageCollection> Handle(PatchImages request, CancellationToken cancellationToken)
        {
            return await dlcsClient.PatchImages(request.Images, request.SpaceId);
        }
    }
}