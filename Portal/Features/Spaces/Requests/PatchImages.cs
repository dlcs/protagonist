using System.Threading;
using System.Threading.Tasks;
using API.Client;
using API.Client.OldJsonLd;
using Hydra.Collections;
using MediatR;
using Image = DLCS.HydraModel.Image;

namespace Portal.Features.Spaces.Requests
{
    public class PatchImages : IRequest<HydraCollection<Image>>
    {
        public HydraCollection<Image> Images { get; set; }
        public int SpaceId { get; set; }
    }

    public class PatchImagesHandler : IRequestHandler<PatchImages, HydraCollection<Image>>
    {
        private readonly IDlcsClient dlcsClient;

        public PatchImagesHandler(IDlcsClient dlcsClient)
        {
            this.dlcsClient = dlcsClient;
        }
        
        
        public async Task<HydraCollection<Image>> Handle(PatchImages request, CancellationToken cancellationToken)
        {
            return await dlcsClient.PatchImages(request.Images, request.SpaceId);
        }
    }
}