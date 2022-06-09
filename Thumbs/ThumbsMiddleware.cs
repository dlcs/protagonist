using System;
using System.Threading.Tasks;
using DLCS.Core.Collections;
using DLCS.Core.Strings;
using DLCS.Model.Assets;
using DLCS.Web.IIIF;
using DLCS.Web.Requests.AssetDelivery;
using DLCS.Web.Response;
using IIIF;
using IIIF.Serialisation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;
using Version = IIIF.ImageApi.Version;

namespace Thumbs
{
    public class ThumbsMiddleware
    {
        private readonly int cacheSeconds;
        private readonly ILogger<ThumbsMiddleware> logger;
        private readonly ThumbnailHandler thumbnailHandler;
        private readonly IThumbRepository thumbRepository;
        private readonly IAssetPathGenerator pathGenerator;
        private readonly Version defaultImageApiVersion;

        public ThumbsMiddleware(
            RequestDelegate next,
            IConfiguration configuration,
            ILogger<ThumbsMiddleware> logger,
            ThumbnailHandler thumbnailHandler,
            IAssetPathGenerator pathGenerator, 
            IThumbRepository thumbRepository)
        {
            // TODO - change this to use CacheSettings
            this.cacheSeconds = configuration.GetValue<int>("ResponseCacheSeconds", 0);
            this.logger = logger;
            this.thumbnailHandler = thumbnailHandler;
            this.pathGenerator = pathGenerator;
            this.thumbRepository = thumbRepository;

            var defaultImageVersion = configuration.GetValue("DefaultIIIFImageVersion", "3");
            defaultImageApiVersion = defaultImageVersion[0] == '2' ? Version.V2 : Version.V3;
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
                await thumbnailHandler.GetThumbnail(request.GetAssetId(), request.IIIFImageRequest);
            
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

        private async Task WriteInfoJson(HttpContext context, ImageAssetDeliveryRequest request)
        {
            var sizes = await thumbRepository.GetOpenSizes(request.GetAssetId());
            if (sizes.IsNullOrEmpty())
            {
                await StatusCodeResponse
                    .NotFound("Could not find requested thumbnail")
                    .WriteJsonResponse(context.Response);
                return;
            }
            
            var requestedVersion = DiscoverRequestedImageApiVersion(context, request);
            
            context.Response.ContentType = context.Request.GetIIIFContentType(requestedVersion);
            context.Response.Headers[HeaderNames.Vary] = new[] { "Accept-Encoding" };
            SetCacheControl(context);
            
            var displayUrl = pathGenerator.GetFullPathForRequest(request);
            
            var id = displayUrl[..displayUrl.LastIndexOf("/", StringComparison.CurrentCultureIgnoreCase)];
            JsonLdBase infoJsonText = requestedVersion == Version.V2
                ? InfoJsonBuilder.GetImageApi2_1Level0(id, sizes)
                : InfoJsonBuilder.GetImageApi3_Level0(id, sizes);
            await context.Response.WriteAsync(infoJsonText.AsJson());
        }

        private Version DiscoverRequestedImageApiVersion(HttpContext context, ImageAssetDeliveryRequest request)
        {
            // If the request has /v2/ or /v3/ before customer etc use that to determine image api version
            if (request.VersionPathValue.HasText())
            {
                return request.VersionPathValue == "v2" ? Version.V2 : Version.V3;
            }

            // Else look at any Accepts headers, falling back to configured version
            return context.Request.GetIIIFImageApiVersion(defaultImageApiVersion);
        }

        private Task RedirectToInfoJson(HttpContext context, ImageAssetDeliveryRequest imageAssetDeliveryRequest)
        {
            var redirectPath = pathGenerator.GetPathForRequest(imageAssetDeliveryRequest);
            if (!redirectPath.EndsWith('/'))
            {
                redirectPath += "/";
            }

            var infoJson = $"{redirectPath}info.json";
            context.Response.SeeOther(infoJson);
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
