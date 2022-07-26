using System.Threading;
using System.Threading.Tasks;
using DLCS.Web.Requests.AssetDelivery;
using MediatR;

namespace Orchestrator.Infrastructure.Mediatr;

/// <summary>
/// Handles <see cref="IAssetRequest"/> requests and populates AssetRequest property
/// </summary>
/// <typeparam name="TRequest">Type of mediatr request</typeparam>
/// <typeparam name="TResponse">Type of response</typeparam>
public class AssetRequestParsingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>, IAssetRequest
{
    private readonly IAssetDeliveryPathParser assetDeliveryPathParser;

    public AssetRequestParsingBehavior(IAssetDeliveryPathParser assetDeliveryPathParser)
    {
        this.assetDeliveryPathParser = assetDeliveryPathParser;
    }
    
    public async Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken,
        RequestHandlerDelegate<TResponse> next)
    {
        switch (request)
        {
            case IImageRequest imageRequest:
                imageRequest.AssetRequest =
                    await assetDeliveryPathParser.Parse<ImageAssetDeliveryRequest>(request.FullPath);
                break;
            case IFileRequest fileRequest:
                fileRequest.AssetRequest =
                    await assetDeliveryPathParser.Parse<FileAssetDeliveryRequest>(request.FullPath);
                break;
            case IGenericAssetRequest genericRequest:
                genericRequest.AssetRequest =
                    await assetDeliveryPathParser.Parse<BaseAssetRequest>(request.FullPath);
                break;
        }

        // TODO - error handling
        // TODO - timeBased handling
        return await next();
    }
}