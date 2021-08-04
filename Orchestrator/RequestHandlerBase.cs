using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DLCS.Web.Requests.AssetDelivery;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Orchestrator.ReverseProxy;

namespace Orchestrator
{
    public abstract class RequestHandlerBase
    {
        protected readonly ILogger Logger;
        protected readonly IAssetTracker AssetTracker;
        protected readonly IAssetDeliveryPathParser AssetDeliveryPathParser;

        public RequestHandlerBase(
            ILogger logger,
            IAssetTracker assetTracker,
            IAssetDeliveryPathParser assetDeliveryPathParser)
        {
            this.Logger = logger;
            this.AssetTracker = assetTracker;
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

        protected async Task<TrackedAsset> GetAsset(ImageAssetDeliveryRequest imageAssetRequest)
        {
            var imageId = imageAssetRequest.GetAssetImageId();
            var asset = await AssetTracker.GetAsset(imageId);
            return asset;
        }
    }
}