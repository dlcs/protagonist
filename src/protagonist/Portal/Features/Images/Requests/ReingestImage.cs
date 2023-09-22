using System.Threading;
using System.Threading.Tasks;
using API.Client;
using DLCS.HydraModel;
using MediatR;

namespace Portal.Features.Images.Requests;

public class ReingestImage : IRequest<Image>
{
    public int SpaceId { get; set; }
    
    public string ImageId { get; set; }
}

public class ReingestImageHandler : IRequestHandler<ReingestImage, Image>
{
    private readonly IDlcsClient dlcsClient;
    
    public ReingestImageHandler(IDlcsClient dlcsClient)
    {
        this.dlcsClient = dlcsClient;
    }

    public async Task<Image> Handle(ReingestImage request, CancellationToken cancellationToken)
    {
        return await dlcsClient.ReingestImage(request.SpaceId, request.ImageId);
    }
}