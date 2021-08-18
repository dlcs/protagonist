using System;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Assets;
using DLCS.Web.Requests.AssetDelivery;
using DLCS.Web.Response;
using MediatR;
using Orchestrator.Assets;
using Orchestrator.Infrastructure.Mediatr;

namespace Orchestrator.Features.Images.Requests
{
    /// <summary>
    /// Mediatr request for generating info.json request for specified image.
    /// </summary>
    public class GetImageInfoJson : IRequest<string?>, IImageRequest
    {
        public string FullPath { get; }
        
        public ImageAssetDeliveryRequest AssetRequest { get; set; }

        public GetImageInfoJson(string path)
        {
            FullPath = path;
        }
    }
    
    public class GetImageInfoJsonHandler : IRequestHandler<GetImageInfoJson, string?>
    {
        private readonly IAssetTracker assetTracker;
        private readonly IAssetPathGenerator assetPathGenerator;

        public GetImageInfoJsonHandler(
            IAssetTracker assetTracker,
            IAssetPathGenerator assetPathGenerator)
        {
            this.assetTracker = assetTracker;
            this.assetPathGenerator = assetPathGenerator;
        }
        
        public async Task<string?> Handle(GetImageInfoJson request, CancellationToken cancellationToken)
        {
            var asset = await assetTracker.GetOrchestrationAsset<OrchestrationImage>(request.AssetRequest.GetAssetId());
            if (asset == null)
            {
                return null;
            }

            var id = assetPathGenerator.GetFullPathForRequest(request.AssetRequest);

            return InfoJsonBuilder.GetImageApi2_1Level1(id, asset.OpenThumbs);
        }
    }
}