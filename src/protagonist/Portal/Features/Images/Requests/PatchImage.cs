using System.Threading;
using System.Threading.Tasks;
using API.Client;
using DLCS.HydraModel;
using MediatR;

namespace Portal.Features.Images.Requests;

public class PatchImage : IRequest<Image>
{
    public int SpaceId { get; set; }
    
    public string ImageId { get; set; }
    
    public Image Image { get; set; }
}

public class PatchImageHandler : IRequestHandler<PatchImage, Image>
{
    private readonly IDlcsClient dlcsClient;
    
    public PatchImageHandler(IDlcsClient dlcsClient)
    {
        this.dlcsClient = dlcsClient;
    }

    public async Task<Image> Handle(PatchImage request, CancellationToken cancellationToken)
    {
        return await dlcsClient.PatchImage(request.Image, request.SpaceId, request.ImageId);
    }
}