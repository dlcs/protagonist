using System.Threading;
using System.Threading.Tasks;
using API.Client;
using DLCS.HydraModel;
using MediatR;

namespace Portal.Features.Spaces.Requests;

public class GetImage : IRequest<Image>
{
    public int SpaceId { get; set; }
    public string ImageId { get; set; }
}

public class GetImageHandler : IRequestHandler<GetImage, Image>
{
    private readonly IDlcsClient dlcsClient;

    public GetImageHandler(IDlcsClient dlcsClient)
    {
        this.dlcsClient = dlcsClient;
    }
    
    public async Task<Image> Handle(GetImage request, CancellationToken cancellationToken)
    {
        return await dlcsClient.GetImage(request.SpaceId, request.ImageId);
    }
}