using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DLCS.Model.Assets;
using DLCS.Web.Requests.AssetDelivery;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Orchestrator
{
    public abstract class RequestHandlerBase
    {
        protected readonly ILogger Logger;
        protected readonly IAssetRepository AssetRepository;
        protected readonly IAssetDeliveryPathParser AssetDeliveryPathParser;

        public RequestHandlerBase(
            ILogger logger,
            IAssetRepository assetRepository,
            IAssetDeliveryPathParser assetDeliveryPathParser)
        {
            this.Logger = logger;
            this.AssetRepository = assetRepository;
            this.AssetDeliveryPathParser = assetDeliveryPathParser;
        }

        protected async Task<(T? assetRequest, HttpStatusCode? statusCode)> TryGetAssetDeliveryRequest<T>(
            HttpContext httpContext) where T : BaseAssetRequest, new()
        {
            try
            {
                var assetRequest =
                    await AssetDeliveryPathParser.Parse<T>(httpContext.Request.Path);
                return (assetRequest, null);
            }
            catch (KeyNotFoundException ex)
            {
                Logger.LogError(ex, "Could not find Customer/Space from '{Path}'", httpContext.Request.Path);
                return (null, HttpStatusCode.NotFound);
            }
            catch (FormatException ex)
            {
                Logger.LogError(ex, "Error parsing path '{Path}'", httpContext.Request.Path);
                return (null, HttpStatusCode.BadRequest);
            }
            catch (Exception ex)
            {
                // TODO - is this the correct status?
                Logger.LogError(ex, "Error parsing path '{Path}'", httpContext.Request.Path);
                return (null, HttpStatusCode.InternalServerError);
            }
        }

        protected async Task<Asset> GetAsset(ImageAssetDeliveryRequest imageAssetRequest)
        {
            var imageId = imageAssetRequest.GetAssetImageId();
            var asset = await AssetRepository.GetAsset(imageId.ToString());
            return asset;
        }

        protected bool DoesAssetRequireAuth(Asset asset) => !string.IsNullOrWhiteSpace(asset.Roles);
    }
}