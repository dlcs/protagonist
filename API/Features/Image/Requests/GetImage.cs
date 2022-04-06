using System.Threading;
using System.Threading.Tasks;
using API.Features.Space.Requests;
using DLCS.Repository;
using MediatR;

namespace API.Features.Image.Requests
{
    public class GetImage : IRequest<DLCS.Model.Assets.Asset>
    {
        public GetImage(int customerId, int spaceId, string modelId)
        {
            CustomerId = customerId;
            SpaceId = spaceId;
            ModelId = modelId;
        }
        
        public int CustomerId { get; private set; }
        public int SpaceId { get; private set; }
        public string ModelId { get; private set; }
        
    }
    
    public class GetImageHandler : IRequestHandler<GetImage, DLCS.Model.Assets.Asset?>
    {
        private readonly DlcsContext dbContext;

        public GetImageHandler(DlcsContext dlcsContext)
        {
            this.dbContext = dlcsContext;
        }
        
        public async Task<DLCS.Model.Assets.Asset?> Handle(GetImage request, CancellationToken cancellationToken)
        {
            var key = $"{request.CustomerId}/{request.SpaceId}/{request.ModelId}";
            var image = await ImageRequestHelpers.GetImageInternal(
                dbContext, key, cancellationToken);
            return image;
        }
    }
}