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
using IIIF.Presentation;
using Microsoft.Extensions.Logging;
using Orchestrator.Assets;
using Orchestrator.Infrastructure.IIIF;
using IIIFAuth2 = IIIF.Auth.V2;
using Version = IIIF.ImageApi.Version;

namespace Orchestrator.Features.Images.ImageServer;

/// <summary>
/// Service responsible for orchestrating image, calling IImageServerClient to get info.json and update with
/// required information that image-server will be unaware of (e.g. Auth, Id).
/// </summary>
public class InfoJsonConstructor
{
    // We want to include both Auth1 + 2 on info.json to allow for transition to auth2
    private readonly IIIIFAuthBuilder iiifAuthBuilder;
    private readonly IIIFAuth1Builder iiifAuth1Builder;
    private readonly IImageServerClient imageServerClient;
    private readonly IThumbRepository thumbRepository;
    private readonly ILogger<InfoJsonConstructor> logger;

    public InfoJsonConstructor(
        IIIIFAuthBuilder iiifAuthBuilder,
        IIIFAuth1Builder iiifAuth1Builder,
        IImageServerClient imageServerClient,
        IThumbRepository thumbRepository,
        ILogger<InfoJsonConstructor> logger)
    {
        this.iiifAuthBuilder = iiifAuthBuilder;
        this.imageServerClient = imageServerClient;
        this.thumbRepository = thumbRepository;
        this.logger = logger;
        this.iiifAuth1Builder = iiifAuth1Builder;
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
            var authServices = await GetAuthAllServices(orchestrationImage, cancellationToken);
            imageService.Service ??= new List<IService>(2);
            imageService.Service.AddRange(authServices);
            imageService.EnsureContext(IIIF.Auth.V2.Constants.IIIFAuth2Context);
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
            var authServices = await GetAuth2Service(orchestrationImage, cancellationToken);
            if (authServices != null)
            {
                imageService.Service ??= new List<IService>(1);
                imageService.Service.Add(authServices);
                imageService.EnsureContext(IIIF.Auth.V2.Constants.IIIFAuth2Context);
            }
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

    private async Task<List<IService>> GetAuthAllServices(OrchestrationImage orchestrationImage, CancellationToken cancellationToken)
    {
        var getAuthServicesForAsset = GetAuth2Service(orchestrationImage, cancellationToken);
        var getAuthCookieService = iiifAuth1Builder.GetAuthServicesForAsset(orchestrationImage.AssetId,
            orchestrationImage.Roles, cancellationToken);

        await Task.WhenAll(getAuthServicesForAsset, getAuthCookieService);

        var returnList = new List<IService>(2);
        if (getAuthServicesForAsset.Result != null)
        {
            returnList.Add(getAuthServicesForAsset.Result);
        }

        if (getAuthCookieService.Result != null)
        {
            returnList.Add(getAuthCookieService.Result);
        }
        else
        {
            logger.LogWarning("{AssetId} requires auth but no auth 1 services generated", orchestrationImage.AssetId);
        }

        return returnList;
    }

    private async Task<IService?> GetAuth2Service(OrchestrationImage orchestrationImage,
        CancellationToken cancellationToken)
    {
        var authServicesForAsset = await iiifAuthBuilder.GetAuthServicesForAsset(orchestrationImage.AssetId,
            orchestrationImage.Roles, cancellationToken);

        if (authServicesForAsset == null)
        {
            logger.LogWarning("{AssetId} requires auth but no auth 2 services generated", orchestrationImage.AssetId);
        }

        return authServicesForAsset;
    }
}