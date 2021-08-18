using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Customer;
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
        private readonly ICustomerOriginStrategyRepository customerOriginStrategyRepository;
        private readonly OriginStrategyResolver originStrategyResolver;

        public GetFileHandler(
            IAssetTracker assetTracker,
            ICustomerOriginStrategyRepository customerOriginStrategyRepository,
            OriginStrategyResolver originStrategyResolver)
        {
            this.assetTracker = assetTracker;
            this.customerOriginStrategyRepository = customerOriginStrategyRepository;
            this.originStrategyResolver = originStrategyResolver;
        }
        
        public async Task<OriginResponse> Handle(GetFile request, CancellationToken cancellationToken)
        {
            var asset = await assetTracker.GetOrchestrationAsset(request.AssetRequest.GetAssetImageId());
            if (asset == null)
            {
                return OriginResponse.Empty;
            }

            var customerOriginStrategy =
                await customerOriginStrategyRepository.GetCustomerOriginStrategy(asset.AssetId, asset.Origin);

            var originStrategy = originStrategyResolver(customerOriginStrategy.Strategy);

            var assetFromOrigin = await originStrategy.LoadAssetFromOrigin(asset.AssetId, asset.Origin,
                customerOriginStrategy, cancellationToken);
            return assetFromOrigin;
        }
    }
}