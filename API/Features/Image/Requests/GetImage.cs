using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Types;
using DLCS.Repository;
using MediatR;

namespace API.Features.Image.Requests
{
    public class GetImage : IRequest<DLCS.Model.Assets.Asset>
    {
        public GetImage(AssetId assetId)
        {
            AssetId = assetId;
        }
        
        public AssetId AssetId { get; private set; }
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
            var image = await ImageRequestHelpers.GetImageInternal(
                dbContext, request.AssetId.ToString(), cancellationToken);
            return image;
        }
    }
}