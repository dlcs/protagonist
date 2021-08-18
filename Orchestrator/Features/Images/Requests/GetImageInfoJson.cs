using System.Threading;
using System.Threading.Tasks;
using DLCS.Core;
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
    public class GetImageInfoJson : IRequest<ImageInfoJsonResponse>, IImageRequest
    {
        public string FullPath { get; }
        
        public ImageAssetDeliveryRequest AssetRequest { get; set; }

        public GetImageInfoJson(string path)
        {
            FullPath = path;
        }
    }

    public class ImageInfoJsonResponse
    {
        public string? InfoJson { get; }
        public bool HasInfoJson { get; }
        public bool RequiresAuth { get; }

        public static ImageInfoJsonResponse Empty = new();

        private ImageInfoJsonResponse()
        {
        }

        public ImageInfoJsonResponse(string infoJson, bool requiresAuth)
        {
            InfoJson = infoJson;
            RequiresAuth = requiresAuth;
            HasInfoJson = true;
        }
    }

    public class GetImageInfoJsonHandler : IRequestHandler<GetImageInfoJson, ImageInfoJsonResponse>
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
        
        public async Task<ImageInfoJsonResponse> Handle(GetImageInfoJson request, CancellationToken cancellationToken)
        {
            var asset = await assetTracker.GetOrchestrationAsset<OrchestrationImage>(request.AssetRequest.GetAssetId());
            if (asset == null)
            {
                return ImageInfoJsonResponse.Empty;
            }

            var imageId = GetImageId(request);

            var infoJson = InfoJsonBuilder.GetImageApi2_1Level1(imageId, asset.Width, asset.Height, asset.OpenThumbs);
            return new ImageInfoJsonResponse(infoJson, asset.RequiresAuth);
        }

        private string GetImageId(GetImageInfoJson request)
            => assetPathGenerator.GetFullPathForRequest(request.AssetRequest,
                (assetRequest, template) => DlcsPathHelpers.GeneratePathFromTemplate(
                    template,
                    assetRequest.RoutePrefix,
                    assetRequest.CustomerPathValue,
                    assetRequest.Space.ToString(),
                    assetRequest.AssetId));
    }
}