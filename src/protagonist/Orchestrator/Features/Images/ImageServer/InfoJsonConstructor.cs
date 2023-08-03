using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Collections;
using DLCS.Model.Assets;
using IIIF;
using IIIF.ImageApi.V2;
using IIIF.ImageApi.V3;
using Microsoft.Extensions.Logging;
using Orchestrator.Assets;
using Orchestrator.Infrastructure.IIIF;
using Version = IIIF.ImageApi.Version;

namespace Orchestrator.Features.Images.ImageServer;

/// <summary>
/// Service responsible for orchestrating image, calling IImageServerClient to get info.json and update with
/// required information that image-server will be unaware of (e.g. Auth, Id).
/// </summary>
public class InfoJsonConstructor
{
    private readonly IIIIFAuthBuilder iiifAuthBuilder;
    private readonly IImageServerClient imageServerClient;
    private readonly IThumbRepository thumbRepository;
    private readonly ILogger<InfoJsonConstructor> logger;

    public InfoJsonConstructor(
        IIIIFAuthBuilder iiifAuthBuilder,
        IImageServerClient imageServerClient,
        IThumbRepository thumbRepository,
        ILogger<InfoJsonConstructor> logger)
    {
        this.iiifAuthBuilder = iiifAuthBuilder;
        this.imageServerClient = imageServerClient;
        this.thumbRepository = thumbRepository;
        this.logger = logger;
    }

    public async Task<JsonLdBase?> BuildInfoJsonFromImageServer(OrchestrationImage orchestrationImage,
        IIIF.ImageApi.Version version,
        CancellationToken cancellationToken = default)
    {
        var getSizesTask = GetSizes(orchestrationImage);

        // Get info.json from downstream image server and add dlcs-known elements (services, thumbs) to it
        // TODO - handle 501 etc from downstream image-server
        if (version == Version.V2)
        {
            var imageServer2 =
                await imageServerClient.GetInfoJson<ImageService2>(orchestrationImage, version, cancellationToken);
            if (imageServer2 == null) return null;
            await UpdateImageService(imageServer2, orchestrationImage, cancellationToken);
            var sizes = await getSizesTask;
            if (!sizes.IsNullOrEmpty())
            {
                imageServer2.Sizes = sizes;
            }
            return imageServer2;
        }
        else
        {
            var imageServer3 =
                await imageServerClient.GetInfoJson<ImageService3>(orchestrationImage, version, cancellationToken);
            if (imageServer3 == null) return null;
            await UpdateImageService(imageServer3, orchestrationImage, cancellationToken);
            var sizes = await getSizesTask;
            if (!sizes.IsNullOrEmpty())
            {
                imageServer3.Sizes = sizes;
            }
            return imageServer3;
        }
    }

    private async Task UpdateImageService(ImageService2? imageService, OrchestrationImage orchestrationImage, 
        CancellationToken cancellationToken)
    {
        if (imageService == null) return;
            
        // Placeholder, will be rewritten on way out
        imageService.Id = $"v2/{orchestrationImage.AssetId}";

        if (orchestrationImage.RequiresAuth && !orchestrationImage.Roles.IsNullOrEmpty())
        {
            var authServicesForAsset =
                await iiifAuthBuilder.GetAuthServicesForAsset(orchestrationImage, cancellationToken);
            if (authServicesForAsset == null)
            {
                logger.LogWarning("{AssetId} requires auth but no auth services generated", orchestrationImage.AssetId);
                return;
            }
            
            imageService.Service ??= new List<IService>(1);
            imageService.Service.Add(authServicesForAsset);
        }
    }

    private async Task UpdateImageService(ImageService3? imageService, OrchestrationImage orchestrationImage, 
        CancellationToken cancellationToken)
    {
        if (imageService == null) return;
            
        // Placeholder, will be rewritten on way out
        imageService.Id = $"v3/{orchestrationImage.AssetId}";

        if (orchestrationImage.RequiresAuth && !orchestrationImage.Roles.IsNullOrEmpty())
        {
            var authCookieServiceForAsset =
                await iiifAuthBuilder.GetAuthServicesForAsset(orchestrationImage, cancellationToken);
            if (authCookieServiceForAsset == null)
            {
                logger.LogWarning("{AssetId} requires auth but no auth services generated", orchestrationImage.AssetId);
                return;
            }
            
            imageService.Service ??= new List<IService>(1);
            imageService.Service.Add(authCookieServiceForAsset);
        }
    }

    private async Task<List<Size>> GetSizes(OrchestrationImage orchestrationImage)
    {
        try
        {
            var thumbs = await thumbRepository.GetAllSizes(orchestrationImage.AssetId);

            if (thumbs.IsNullOrEmpty())
            {
                logger.LogInformation("No thumbnails found for {Asset}", orchestrationImage.AssetId);
                return Enumerable.Empty<Size>().ToList();
            }
            
            return thumbs.Select(s => Size.FromArray(s)).ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting size for info.json for {Asset}", orchestrationImage.AssetId);
            return Enumerable.Empty<Size>().ToList();
        }
    }
}