using System;
using System.Threading.Tasks;
using DLCS.Core.Collections;
using DLCS.Model.Assets;
using DLCS.Web.Requests.AssetDelivery;
using DLCS.Web.Response;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;

namespace Thumbs
{
    public class ThumbsMiddleware
    {
        private readonly int cacheSeconds;
        private readonly ILogger<ThumbsMiddleware> logger;
        private readonly IThumbRepository thumbRepository;
        private readonly IAssetPathGenerator pathGenerator;

        public ThumbsMiddleware(
            RequestDelegate next,
            IConfiguration configuration,
            ILogger<ThumbsMiddleware> logger,
            IThumbRepository thumbRepository,
            IAssetPathGenerator pathGenerator)
        {
            this.cacheSeconds = configuration.GetValue<int>("ResponseCacheSeconds", 0);
            this.logger = logger;
            this.thumbRepository = thumbRepository;
            this.pathGenerator = pathGenerator;
        }

        public async Task Invoke(HttpContext context,
            AssetDeliveryPathParser parser)
        {
            try
            {
                var thumbnailRequest = await parser.Parse<ImageAssetDeliveryRequest>(context.Request.Path.Value);
                if (thumbnailRequest.IIIFImageRequest.IsBase)
                {
                    await RedirectToInfoJson(context, thumbnailRequest);
                }
                else if (thumbnailRequest.IIIFImageRequest.IsInformationRequest)
                {
                    await WriteInfoJson(context, thumbnailRequest);
                }
                else
                {
                    // mode for debugging etc
                    switch (context.Request.Query["mode"])
                    {
                        case "dump":
                            await WriteRequestDump(context, thumbnailRequest);
                            break;
                        default:
                            await WritePixels(context, thumbnailRequest);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing request for request {Path}", context.Request.Path);
                throw;
            }
        }

        private async Task WritePixels(HttpContext context, ImageAssetDeliveryRequest request)
        {
            await using var thumbnailResponse =
                await thumbRepository.GetThumbnail(request.Customer.Id, request.Space, request.IIIFImageRequest);
            
            if (thumbnailResponse.IsEmpty)
            {
                await StatusCodeResponse
                    .NotFound("Could not find requested thumbnail")
                    .WriteJsonResponse(context.Response);
            }
            else
            {
                context.Response.ContentType = "image/jpeg";
                SetCacheControl(context);
                SetResponseHeaders(context, thumbnailResponse);
                if (thumbnailResponse.ThumbnailStream!.CanSeek)
                {
                    thumbnailResponse.ThumbnailStream.Position = 0;
                }

                await thumbnailResponse.ThumbnailStream.CopyToAsync(context.Response.Body);
            }
        }

        private static async Task WriteRequestDump(HttpContext context, ImageAssetDeliveryRequest request)
        {
            await context.Response.WriteAsync(JsonConvert.SerializeObject(request));
        }

        // TODO: observe Accepts header - https://iiif.io/api/image/3.0/#51-image-information-request
        // TODO: Don't construct the URL from what came in on the Request.
        private async Task WriteInfoJson(HttpContext context, ImageAssetDeliveryRequest request)
        {
            var sizes = await thumbRepository.GetOpenSizes(request.Customer.Id, request.Space, request.IIIFImageRequest);
            if (sizes.IsNullOrEmpty())
            {
                await StatusCodeResponse
                    .NotFound("Could not find requested thumbnail")
                    .WriteJsonResponse(context.Response);
                return;
            }
            
            context.Response.ContentType = "application/json";
            context.Response.Headers[HeaderNames.Vary] = new[] { "Accept-Encoding" };
            SetCacheControl(context);
            
            var displayUrl = pathGenerator.GetFullPathForRequest(request);
            
            var id = displayUrl.Substring(0, displayUrl.LastIndexOf("/", StringComparison.CurrentCultureIgnoreCase));
            var infoJsonText = InfoJsonBuilder.GetImageApi2_1(id, sizes);
            await context.Response.WriteAsync(infoJsonText);
        }

        private Task RedirectToInfoJson(HttpContext context, ImageAssetDeliveryRequest imageAssetDeliveryRequest)
        {
            var redirectPath = pathGenerator.GetPathForRequest(imageAssetDeliveryRequest);
            if (!redirectPath.EndsWith('/'))
            {
                redirectPath += "/";
            }

            var infoJson = $"{redirectPath}info.json";
            context.Response.Redirect(infoJson);
            return context.Response.CompleteAsync();
        }

        private void SetCacheControl(HttpContext context)
        {
            if (cacheSeconds > 0)
            {
                context.Response.GetTypedHeaders().CacheControl =
                    new CacheControlHeaderValue
                    {
                        Public = true,
                        MaxAge = TimeSpan.FromSeconds(cacheSeconds)
                    };
            }
        }

        private void SetResponseHeaders(HttpContext context, ThumbnailResponse thumbnailResponse)
        {
            if (thumbnailResponse.WasResized)
            {
                context.Response.Headers.Add("x-thumb-resized", "1");
            }
            
            if (thumbnailResponse.IsExactMatch)
            {
                context.Response.Headers.Add("x-thumb-match", "1");
            }
        }
    }
}
