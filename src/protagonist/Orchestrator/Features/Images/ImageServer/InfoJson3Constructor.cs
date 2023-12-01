using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Assets;
using IIIF;
using IIIF.ImageApi;
using IIIF.ImageApi.V3;
using Microsoft.Extensions.Logging;
using Orchestrator.Assets;
using Orchestrator.Infrastructure.IIIF;
using Version = IIIF.ImageApi.Version;

namespace Orchestrator.Features.Images.ImageServer;

/// <summary>
/// Implementation of <see cref="InfoJsonConstructorTemplate{T}"/> responsible for building IIIF ImageService3
/// info.json. Assets requiring auth will have the following updates:
///  If Roles are present, Auth v2 services are added + context updated
///  If no Roles (ie MaxUnauthorised only) the maxWidth property is set  
/// </summary>
public class InfoJson3Constructor : InfoJsonConstructorTemplate<ImageService3>
{
    protected override Version ImageApiVersion => Version.V3;

    public InfoJson3Constructor(
        IIIIFAuthBuilder iiifAuthBuilder,
        IImageServerClient imageServerClient,
        IThumbRepository thumbRepository,
        ILogger<InfoJson3Constructor> logger) : base(imageServerClient, thumbRepository, iiifAuthBuilder, logger)
    {
    }

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
        imageService.MaxWidth = orchestrationImage.MaxUnauthorised;
    }

    protected override void SetImageServiceStubId(ImageService3 imageService, OrchestrationImage orchestrationImage) 
        => imageService.Id = $"v3/{orchestrationImage.AssetId}";

    protected override void SetImageServiceSizes(ImageService3 imageService, List<Size> sizes) 
        => imageService.Sizes = sizes;

    protected override void SetImageTileServiceSizes(ImageService3 imageService, int maxUnauthorised)
    {
        if (imageService.Tiles == null || imageService.Tiles.Select(s => s.Width).Max() > maxUnauthorised)
        {
            // This code is working out the max tiles size based on max unauthorised.
            // The tile size must be a power of 2 and less than maxUnauthorised
            // for example, if maxUnauthorised is 500, the tile size will be updated to 256
            var tileSize =
                Math.Pow(2, (int)Math.Log2(maxUnauthorised)); // Casting as it truncates

            var tiles = InfoJsonBuilder.GetTiles(imageService.Width, imageService.Height,
                (int)tileSize);

            imageService.Tiles = tiles;
        }
    }
}