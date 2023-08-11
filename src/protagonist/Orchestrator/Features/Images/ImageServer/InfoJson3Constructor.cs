using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Assets;
using IIIF;
using IIIF.ImageApi;
using IIIF.ImageApi.V3;
using Microsoft.Extensions.Logging;
using Orchestrator.Assets;
using Orchestrator.Infrastructure.IIIF;

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
}