using System.Threading;
using System.Threading.Tasks;
using DLCS.Repository.Strategy;
using DLCS.Web.Requests.AssetDelivery;
using MediatR;
using Orchestrator.Assets;
using Orchestrator.Infrastructure.Mediatr;

namespace Orchestrator.Features.Files.Requests
{
    /// <summary>
    /// Mediatr request for loading file from origin
    /// </summary>
    public class GetFile : IRequest<OriginResponse>, IFileRequest
    {
        public string FullPath { get; }
        
        public FileAssetDeliveryRequest? AssetRequest { get; set; }

        public GetFile(string path)
        {
            FullPath = path;
        }
    }
    
    public class GetFileHandler : IRequestHandler<GetFile, OriginResponse>
    {
        private readonly IAssetTracker assetTracker;
        private readonly OriginFetcher originFetcher;

        public GetFileHandler(IAssetTracker assetTracker, OriginFetcher originFetcher)
        {
            this.assetTracker = assetTracker;
            this.originFetcher = originFetcher;
        }

        public async Task<OriginResponse> Handle(GetFile request, CancellationToken cancellationToken)
        {
            var asset = await assetTracker.GetOrchestrationAsset<OrchestrationFile>(request.AssetRequest.GetAssetId());
            if (asset == null)
            {
                return OriginResponse.Empty;
            }

            var assetFromOrigin =
                await originFetcher.LoadAssetFromLocation(asset.AssetId, asset.Origin, cancellationToken);
            return assetFromOrigin;
        }
    }
}