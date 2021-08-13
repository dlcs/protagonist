using System.Threading;
using System.Threading.Tasks;
using DLCS.Web.Requests.AssetDelivery;
using MediatR;

namespace Orchestrator.Infrastructure.Mediatr
{
    /// <summary>
    /// Handles <see cref="IAssetRequest{T}"/> requests and populates AssetRequest property
    /// </summary>
    /// <typeparam name="TRequest">Type of mediatr request</typeparam>
    /// <typeparam name="TResponse">Type of response</typeparam>
    /// <typeparam name="TAssetRequest">Type of AssetRequest being made</typeparam>
    /*public class AssetRequestParsingBehavior<TRequest, TResponse, TAssetRequest> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IAssetRequest<TAssetRequest>
        where TAssetRequest : BaseAssetRequest, new()*/
    public class AssetRequestParsingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IAssetRequest
    {
        private readonly IAssetDeliveryPathParser assetDeliveryPathParser;

        public AssetRequestParsingBehavior(IAssetDeliveryPathParser assetDeliveryPathParser)
        {
            this.assetDeliveryPathParser = assetDeliveryPathParser;
        }
        
        public async Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken,
            RequestHandlerDelegate<TResponse> next)
        {
            if (request is IFileRequest fileRequest)
            {
                fileRequest.AssetRequest = await assetDeliveryPathParser.Parse<FileAssetDeliveryRequest>(request.FullPath);    
            }
            // TODO - error handling
            // TODO - image and TimeBased handling
            return await next();
        }
    }
}