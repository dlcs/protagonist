using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Collections;
using DLCS.Model.Assets;
using IIIF;
using IIIF.ImageApi.V2;
using Microsoft.Extensions.Logging;
using Orchestrator.Assets;
using Orchestrator.Infrastructure.IIIF;
using Version = IIIF.ImageApi.Version;

namespace Orchestrator.Features.Images.ImageServer;

/// <summary>
/// Implementation of <see cref="InfoJsonConstructorTemplate{T}"/> responsible for building IIIF ImageService2
/// info.json. Assets requiring auth will have the following updates:
///  If Roles are present, Auth v0/1 (dependant on DB profiles) and Auth v2 services are added. Only auth2 context added
///  If no Roles (ie MaxUnauthorised only) the profile.maxWidth property is set  
/// </summary>
public class InfoJson2Constructor : InfoJsonConstructorTemplate<ImageService2>
{
    // We want to include both Auth1 + 2 on info.json to allow for transition to auth2
    private readonly IIIFAuth1Builder iiifAuth1Builder;

    public InfoJson2Constructor(
        IIIIFAuthBuilder iiifAuthBuilder,
        IIIFAuth1Builder iiifAuth1Builder,
        IImageServerClient imageServerClient,
        IThumbRepository thumbRepository,
        ILogger<InfoJson2Constructor> logger) : base(imageServerClient, thumbRepository, iiifAuthBuilder, logger)
    {
        this.iiifAuth1Builder = iiifAuth1Builder;
    }

    protected override Version ImageApiVersion => Version.V2;
    
    protected override async Task SetImageServiceAuthServices(ImageService2 imageService, OrchestrationImage orchestrationImage,
        CancellationToken cancellationToken)
    {
        var authServices = await GetAuthAllServices(orchestrationImage, cancellationToken);
        imageService.Service ??= new List<IService>(2);
        imageService.Service.AddRange(authServices);
        imageService.EnsureContext(IIIF.Auth.V2.Constants.IIIFAuth2Context);
    }

    protected override void SetImageServiceMaxWidth(ImageService2 imageService, OrchestrationImage orchestrationImage)
    {
        imageService.ProfileDescription ??= new ProfileDescription();
        imageService.ProfileDescription.MaxArea = null;
        imageService.ProfileDescription.MaxHeight = null;
        imageService.ProfileDescription.MaxWidth = orchestrationImage.MaxUnauthorised;
    }

    protected override void SetImageServiceStubId(ImageService2 imageService, OrchestrationImage orchestrationImage) 
        => imageService.Id = $"v2/{orchestrationImage.AssetId}";

    protected override void SetImageServiceSizes(ImageService2 imageService, List<Size> sizes)
        => imageService.Sizes = sizes;
    
    protected override void SetImageServiceTiles (ImageService2 imageService, OrchestrationImage orchestrationImage)
    {
        if (imageService.Tiles.IsNullOrEmpty() || imageService.Tiles
                .Select(s => s.Width).Max() > orchestrationImage.MaxUnauthorised)
        {
            var tiles = GetTiles(orchestrationImage);

            imageService.Tiles = tiles;
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
            Logger.LogWarning("{AssetId} requires auth but no auth 1 services generated", orchestrationImage.AssetId);
        }

        return returnList;
    }
}