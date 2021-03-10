using System;
using System.Linq;
using System.Threading.Tasks;
using DLCS.Model.Assets;
using DLCS.Model.Storage;
using DLCS.Web.Requests.AssetDelivery;
using DLCS.Web.Response;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
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
            IBucketReader bucketReader,
            AssetDeliveryPathParser parser)
        {
            try
            {
                var headers = string.Join("|", context.Request.Headers.Select(h => $"{h.Key}:{h.Value}"));
                logger.LogInformation("{Url} Headers: {Headers}", context.Request.GetDisplayUrl(), headers);
            }
            catch
            {
                // for debug purposes only
            }

            var thumbnailRequest = await parser.Parse(context.Request.Path.Value);
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
                        await WritePixels(context, thumbnailRequest, bucketReader);
                        break;
                }
            }
        }

        private async Task WritePixels(HttpContext context, ThumbnailRequest request, IBucketReader bucketReader)
        {
            var thumbInBucket = thumbRepository.GetThumbLocation(
                request.Customer.Id, request.Space, request.IIIFImageRequest);
            context.Response.ContentType = "image/jpeg";
            SetCacheControl(context);
            var response = await bucketReader.GetObjectFromBucket(await thumbInBucket);

            if (response == null)
            {
                await StatusCodeResponse
                    .NotFound("Could not find requested thumbnail")
                    .WriteJsonResponse(context.Response);
            }
            else
            {
                await response.CopyToAsync(context.Response.Body);
            }
        }

        private static async Task WriteRequestDump(HttpContext context, ThumbnailRequest request)
        {
            await context.Response.WriteAsync(JsonConvert.SerializeObject(request));
        }

        // TODO: observe Accepts header - https://iiif.io/api/image/3.0/#51-image-information-request
        // TODO: Don't construct the URL from what came in on the Request.
        private async Task WriteInfoJson(HttpContext context, ThumbnailRequest request)
        {
            var sizes = await thumbRepository.GetSizes(request.Customer.Id, request.Space, request.IIIFImageRequest);
            context.Response.ContentType = "application/json";
            context.Response.Headers[HeaderNames.Vary] = new[] { "Accept-Encoding" };
            SetCacheControl(context);
            
            var displayUrl = pathGenerator.GetFullPathForRequest(request);
            
            var id = displayUrl.Substring(0, displayUrl.Length - 10);  // the length of "/info.json" ... yeah
            var infoJsonText = InfoJsonBuilder.GetImageApi2_1(id, sizes);
            await context.Response.WriteAsync(infoJsonText);
        }

        private Task RedirectToInfoJson(HttpContext context, ThumbnailRequest thumbnailRequest)
        {
            var redirectPath = pathGenerator.GetPathForRequest(thumbnailRequest);
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
    }
}
