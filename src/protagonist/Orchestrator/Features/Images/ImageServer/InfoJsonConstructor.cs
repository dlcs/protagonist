using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Collections;
using IIIF;
using IIIF.ImageApi;
using IIIF.ImageApi.V2;
using IIIF.ImageApi.V3;
using Orchestrator.Assets;
using Orchestrator.Infrastructure.IIIF;

namespace Orchestrator.Features.Images.ImageServer;

/// <summary>
/// Service responsible for orchestrating image, calling IImageServerClient to get info.json and update with
/// required information that image-server will be unaware of (e.g. Auth, Id).
/// </summary>
public class InfoJsonConstructor
{
    private readonly IIIFAuthBuilder iiifAuthBuilder;
    private readonly IImageServerClient imageServerClient;

    public InfoJsonConstructor(
        IIIFAuthBuilder iiifAuthBuilder,
        IImageServerClient imageServerClient)
    {
        this.iiifAuthBuilder = iiifAuthBuilder;
        this.imageServerClient = imageServerClient;
    }

    public async Task<JsonLdBase?> BuildInfoJsonFromImageServer(OrchestrationImage orchestrationImage,
        IIIF.ImageApi.Version version,
        CancellationToken cancellationToken = default)
    {
        // Get info.json from downstream image server and add related services to it
        // TODO - handle 501 etc from downstream image-server
        if (version == Version.V2)
        {
            var imageServer2 =
                await imageServerClient.GetInfoJson<ImageService2>(orchestrationImage, version, cancellationToken);
            await UpdateImageService(imageServer2, orchestrationImage, cancellationToken);
            return imageServer2;
        }
        else
        {
            var imageServer3 =
                await imageServerClient.GetInfoJson<ImageService3>(orchestrationImage, version, cancellationToken);
            await UpdateImageService(imageServer3, orchestrationImage, cancellationToken);
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
            var authCookieServiceForAsset =
                await iiifAuthBuilder.GetAuthCookieServiceForAsset(orchestrationImage, cancellationToken);
            if (authCookieServiceForAsset == null) return;
            
            imageService.Service ??= new List<IService>(1);
            imageService.Service.Add(authCookieServiceForAsset);
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
                await iiifAuthBuilder.GetAuthCookieServiceForAsset(orchestrationImage, cancellationToken);
            if (authCookieServiceForAsset == null) return;
            
            imageService.Service ??= new List<IService>(1);
            imageService.Service.Add(authCookieServiceForAsset);
        }
    }
}