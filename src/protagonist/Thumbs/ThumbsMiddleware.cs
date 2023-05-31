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
using Version = IIIF.ImageApi.Version;

namespace Thumbs;

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

        defaultImageApiVersion = configuration.GetValue<Version>("DefaultIIIFImageVersion", Version.V3);
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
                await HandleInfoJsonRequest(context, thumbnailRequest);
            }
            else
            {
                await WritePixels(context, thumbnailRequest);
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
        var imageRequest = request.IIIFImageRequest;
        if (!imageRequest.IsCandidateForThumbHandling(out var errorMessage))
        {
            await StatusCodeResponse
                .BadRequest(errorMessage!)
                .WriteJsonResponse(context.Response);
            return;
        }

        await using var thumbnailResponse =
            await thumbnailHandler.GetThumbnail(request.GetAssetId(), imageRequest);

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

    private async Task HandleInfoJsonRequest(HttpContext context, ImageAssetDeliveryRequest request)
    {
        var (requestedVersion, directRequest) = DiscoverRequestedImageApiVersion(context, request);
        if (IsCanonical(requestedVersion) && directRequest)
        {
            // If request was direct on v2/v3 path and for canonical version - redirect to canonical version
            var infoJson = GetInfoJsonPath(request, requestedVersion);
            context.Response.Redirect(infoJson);
            await context.Response.CompleteAsync();
        }
        else
        {
            await WriteInfoJson(context, request);
        }
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
        
        var (requestedVersion, _) = DiscoverRequestedImageApiVersion(context, request);
        
        context.Response.ContentType = context.Request.GetIIIFContentType(requestedVersion);
        context.Response.Headers[HeaderNames.Vary] = new[] { "Accept-Encoding" };
        SetCacheControl(context);
        
        var id = GetFullImagePath(request, requestedVersion);
        
        JsonLdBase infoJsonText = requestedVersion == Version.V2
            ? InfoJsonBuilder.GetImageApi2_1Level0(id, sizes)
            : InfoJsonBuilder.GetImageApi3_Level0(id, sizes);
        await context.Response.WriteAsync(infoJsonText.AsJson());
    }

    private (Version version, bool directRequest) DiscoverRequestedImageApiVersion(HttpContext context,
        ImageAssetDeliveryRequest request)
    {
        // If the request has /v2/ or /v3/ before customer etc use that to determine image api version
        if (request.VersionPathValue.HasText())
        {
            var iiifImageApiVersion = request.VersionPathValue.ParseToIIIFImageApiVersion();
            if (iiifImageApiVersion.HasValue)
            {
                return (iiifImageApiVersion.Value, true);
            }
        }

        // Else look at any Accepts headers, falling back to configured version
        return (context.Request.GetIIIFImageApiVersion(defaultImageApiVersion), false);
    }

    private Task RedirectToInfoJson(HttpContext context, ImageAssetDeliveryRequest imageAssetDeliveryRequest)
    {
        // If this path is versioned and for the canonical version then redirect to that
        var (requestedVersion, _) = DiscoverRequestedImageApiVersion(context, imageAssetDeliveryRequest);

        var infoJson = GetInfoJsonPath(imageAssetDeliveryRequest, requestedVersion);
        context.Response.SeeOther(infoJson);
        return context.Response.CompleteAsync();
    }

    private string GetInfoJsonPath(ImageAssetDeliveryRequest imageAssetDeliveryRequest, Version requestedVersion)
    {
        var redirectPath = GetFullImagePath(imageAssetDeliveryRequest, requestedVersion);

        var infoJson = redirectPath.ToConcatenated('/', "info.json");
        return infoJson;
    }

    private string GetFullImagePath(ImageAssetDeliveryRequest imageAssetDeliveryRequest, Version requestedVersion)
    {
        var baseRequest = imageAssetDeliveryRequest.CloneBasicPathElements();
        
        // We want the image id only, without "/info.json"
        baseRequest.AssetPath = imageAssetDeliveryRequest.AssetId;

        if (IsCanonical(requestedVersion))
        {
            baseRequest.VersionPathValue = null;
        }
        
        return pathGenerator.GetFullPathForRequest(baseRequest, includeQueryParams: false);
    }

    private bool IsCanonical(Version requestedVersion)
    {
        var isCanonical = requestedVersion == defaultImageApiVersion;
        return isCanonical;
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
