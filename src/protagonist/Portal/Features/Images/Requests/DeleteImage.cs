using System.Threading;
using System.Threading.Tasks;
using API.Client;
using MediatR;

namespace Portal.Features.Images.Requests;

public class DeleteImage : IRequest<bool>
{
    public int SpaceId { get; set; }
    
    public string ImageId { get; set; }
}

public class DeleteImageHandler : IRequestHandler<DeleteImage, bool>
{
    private readonly IDlcsClient dlcsClient;
    
    public DeleteImageHandler(IDlcsClient dlcsClient)
    {
        this.dlcsClient = dlcsClient;
    }

    public async Task<bool> Handle(DeleteImage request, CancellationToken cancellationToken)
    {
        return await dlcsClient.DeleteImage(request.SpaceId, request.ImageId);
    }
}