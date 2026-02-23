using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Assets;
using IIIF;
using IIIF.ImageApi.V3;
using Microsoft.Extensions.Logging;
using Orchestrator.Assets;
using Orchestrator.Infrastructure.IIIF;
using Version = IIIF.ImageApi.Version;

namespace Orchestrator.Features.Images.ImageServer;

/// <summary>
/// Implementation of <see cref="InfoJsonConstructorTemplate{T}"/> responsible for building IIIF ImageService3
/// info.json.
///  If Roles are present, Auth v2 services are added + context updated, unless it is 'unobtainable' role only
///  The maxWidth property is set in all instances  
/// </summary>
public class InfoJson3Constructor(
    IIIIFAuthBuilder iiifAuthBuilder,
    IImageServerClient imageServerClient,
    IThumbRepository thumbRepository,
    ILogger<InfoJson3Constructor> logger)
    : InfoJsonConstructorTemplate<ImageService3>(imageServerClient, thumbRepository, iiifAuthBuilder, logger)
{
    protected override Version ImageApiVersion => Version.V3;

    protected override async Task SetImageServiceAuthServices(ImageService3 imageService, OrchestrationImage orchestrationImage,
        CancellationToken cancellationToken)
    {
        var authServices = await GetAuth2Service(orchestrationImage, cancellationToken);
        if (authServices != null)
        {
            imageService.Service ??= new List<IService>(1);
            imageService.Service.Add(authServices);
            imageService.EnsureContext(IIIF.Auth.V2.Constants.IIIFAuth2Context);
        }
    }

    protected override void SetImageServiceMaxWidth(ImageService3 imageService, OrchestrationImage orchestrationImage)
    {
        imageService.MaxArea = null;
        imageService.MaxHeight = null;
        imageService.MaxWidth = orchestrationImage.MaxWidth;
    }

    protected override void SetImageServiceStubId(ImageService3 imageService, OrchestrationImage orchestrationImage) 
        => imageService.Id = $"v3/{orchestrationImage.AssetId}";

    protected override void SetImageServiceSizes(ImageService3 imageService, List<Size> sizes) 
        => imageService.Sizes = sizes;

    protected override void TrySetImageServiceTiles(ImageService3 imageService, OrchestrationImage orchestrationImage)
    {
        if (!ShouldUpdateTiles(imageService.Tiles, orchestrationImage)) return;
        imageService.Tiles = GetTiles(orchestrationImage);
    }
}
