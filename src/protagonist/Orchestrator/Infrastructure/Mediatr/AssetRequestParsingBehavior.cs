using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Types;
using DLCS.Web.Requests.AssetDelivery;
using DLCS.Web.Response;
using MediatR;
using Microsoft.AspNetCore.Http;

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
    private readonly IHttpContextAccessor httpContextAccessor;

    public AssetRequestParsingBehavior(IAssetDeliveryPathParser assetDeliveryPathParser,
        IHttpContextAccessor httpContextAccessor)
    {
        this.assetDeliveryPathParser = assetDeliveryPathParser;
        this.httpContextAccessor = httpContextAccessor;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next
        , CancellationToken cancellationToken)
    {
        AssetId? assetId = null;
        switch (request)
        {
            case IImageRequest imageRequest:
                var imageAssetDeliveryRequest =
                    await assetDeliveryPathParser.Parse<ImageAssetDeliveryRequest>(request.FullPath);
                assetId = imageAssetDeliveryRequest.GetAssetId();
                imageRequest.AssetRequest =
                    imageAssetDeliveryRequest;
                break;
            case IGenericAssetRequest genericRequest:
                var baseAssetRequest = await assetDeliveryPathParser.Parse<BaseAssetRequest>(request.FullPath);
                assetId = baseAssetRequest.GetAssetId();
                genericRequest.AssetRequest =
                    baseAssetRequest;
                break;
        }

        var response = await next();
        TrySetAssetIdResponseHeader(assetId);
        return response;
    }

    private void TrySetAssetIdResponseHeader(AssetId? assetId)
    {
        if (assetId == null) return;
        httpContextAccessor.HttpContext?.Response.SetAssetIdResponseHeader(assetId);
    }
}
