using System;
using System.Threading.Tasks;
using DLCS.Model.Assets;
using DLCS.Model.Storage;
using DLCS.Web.Requests.AssetDelivery;
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
        private readonly IConfiguration configuration;
        private readonly ILogger<ThumbsMiddleware> logger;
        private readonly IThumbRepository thumbRepository;

        public ThumbsMiddleware(
            RequestDelegate next,
            IConfiguration configuration,
            ILogger<ThumbsMiddleware> logger,
            IThumbRepository thumbRepository)
        {
            this.configuration = configuration;
            this.cacheSeconds = configuration.GetValue<int>("ResponseCacheSeconds", 0);
            this.logger = logger;
            this.thumbRepository = thumbRepository;
        }

        public Task Invoke(HttpContext context,
            IBucketReader bucketReader,
            AssetDeliveryPathParser parser)
        {
            var thumbnailRequest = parser.Parse(context.Request.Path.Value);
            if (thumbnailRequest.IIIFImageRequest.IsBase)
            {
                return RedirectToInfoJson(context);
            }

            if (thumbnailRequest.IIIFImageRequest.IsInformationRequest)
            {
                return WriteInfoJson(context, thumbnailRequest);
            }

            // mode for debugging etc
            switch (context.Request.Query["mode"])
            {
                case "dump":
                    return WriteRequestDump(context, thumbnailRequest);
                default:
                    return WritePixels(context, thumbnailRequest, bucketReader);
            }
        }


        private async Task WritePixels(HttpContext context, ThumbnailRequest request, IBucketReader bucketReader)
        {
            var thumbInBucket = thumbRepository.GetThumbLocation(
                request.Customer.Id, request.Space, request.IIIFImageRequest);
            context.Response.ContentType = "image/jpeg";
            SetCacheControl(context);
            await bucketReader.WriteObjectFromBucket(await thumbInBucket, context.Response.Body);
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

            // TODO - not like this, construct it properly
            var displayUrl = context.Request.GetDisplayUrl();
            var id = displayUrl.Substring(0, displayUrl.Length - 10);  // the length of "/info.json" ... yeah
            var infoJsonText = InfoJsonBuilder.GetImageApi2_1(id, sizes);
            await context.Response.WriteAsync(infoJsonText);
        }


        private static Task RedirectToInfoJson(HttpContext context)
        {
            var thisPath = context.Request.Path.ToString();
            if (!thisPath.EndsWith('/'))
            {
                thisPath += "/";
            }

            var infoJson = thisPath + "info.json";
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
