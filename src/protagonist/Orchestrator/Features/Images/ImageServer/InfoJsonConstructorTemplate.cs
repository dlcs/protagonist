using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Collections;
using DLCS.Model.Assets;
using IIIF;
using IIIF.ImageApi;
using Microsoft.Extensions.Logging;
using Orchestrator.Assets;
using Orchestrator.Infrastructure.IIIF;

namespace Orchestrator.Features.Images.ImageServer;

/// <summary>
/// Template base class responsible for orchestrating image, calling IImageServerClient to get info.json and update with
/// required information that image-server will be unaware of (e.g. Auth, Id).
/// </summary>
public abstract class InfoJsonConstructorTemplate<T> : IInfoJsonConstructor
    where T : JsonLdBase
{
    protected abstract IIIF.ImageApi.Version ImageApiVersion { get; }
    protected readonly ILogger Logger;
    private readonly IImageServerClient imageServerClient;
    private readonly IThumbRepository thumbRepository;
    private readonly IIIIFAuthBuilder iiifAuthBuilder;

    protected InfoJsonConstructorTemplate(
        IImageServerClient imageServerClient,
        IThumbRepository thumbRepository,
        IIIIFAuthBuilder iiifAuthBuilder,
        ILogger logger)
    {
        this.imageServerClient = imageServerClient;
        this.thumbRepository = thumbRepository;
        Logger = logger;
        this.iiifAuthBuilder = iiifAuthBuilder;
    }

    public async Task<JsonLdBase?> BuildInfoJsonFromImageServer(OrchestrationImage orchestrationImage,
        CancellationToken cancellationToken = default)
    {
        var getSizesTask = GetSizes(orchestrationImage);

        // Get info.json from downstream image server and add dlcs-known elements (services, thumbs) to it
        // TODO - handle 501 etc from downstream image-server
        var imageService =
            await imageServerClient.GetInfoJson<T>(orchestrationImage, ImageApiVersion, cancellationToken);
        if (imageService == null) return null;

        await UpdateImageService(imageService, orchestrationImage, cancellationToken);
        var sizes = await getSizesTask;
        if (!sizes.IsNullOrEmpty())
        {
            SetImageServiceSizes(imageService, sizes);
        }

        return imageService;
    }

    private async Task UpdateImageService(T? imageService, OrchestrationImage orchestrationImage,
        CancellationToken cancellationToken)
    {
        if (imageService == null) return;

        SetImageServiceStubId(imageService, orchestrationImage);

        if (orchestrationImage.RequiresAuth)
        {
            if (orchestrationImage.Roles.IsNullOrEmpty())
            {
                Logger.LogDebug("Asset {AssetId} requires auth but no roles, adding maxWidth",
                    orchestrationImage.AssetId);

                SetImageServiceMaxWidth(imageService, orchestrationImage);
            }
            else
            {
                Logger.LogDebug("Asset {AssetId} requires auth with roles, adding auth-services",
                    orchestrationImage.AssetId);
                await SetImageServiceAuthServices(imageService, orchestrationImage, cancellationToken);
            }
        }
        
        if (orchestrationImage.MaxUnauthorised > 0)
        {
            SetImageServiceTiles (imageService, orchestrationImage);
        }
    }

    /// <summary>
    /// Add required auth services to "services" property
    /// </summary>
    protected abstract Task SetImageServiceAuthServices(T imageService, OrchestrationImage orchestrationImage,
        CancellationToken cancellationToken);
    
    /// <summary>
    /// Set maxWidth property on ImageService
    /// </summary>
    protected abstract void SetImageServiceMaxWidth(T imageService, OrchestrationImage orchestrationImage);
    
    /// <summary>
    /// Set the stub Id property, this will be overwritten further downstream
    /// </summary>
    protected abstract void SetImageServiceStubId(T imageService, OrchestrationImage orchestrationImage);
    
    /// <summary>
    /// Overwrite the "sizes" property on info.json with given sizes
    /// </summary>
    protected abstract void SetImageServiceSizes(T imageService, List<Size> sizes);
    
    /// <summary>
    /// Overwrite the "tiles" property on info.json with given tile sizes
    /// </summary>
    /// <param name="imageService">The image service</param>
    /// <param name="orchestrationImage">The image being orchestrated</param>
    protected abstract void SetImageServiceTiles (T imageService, OrchestrationImage orchestrationImage);

    protected async Task<IService?> GetAuth2Service(OrchestrationImage orchestrationImage,
        CancellationToken cancellationToken)
    {
        var authServicesForAsset = await iiifAuthBuilder.GetAuthServicesForAsset(orchestrationImage.AssetId,
            orchestrationImage.Roles, cancellationToken);

        if (authServicesForAsset == null)
        {
            Logger.LogWarning("{AssetId} requires auth but no auth 2 services generated", orchestrationImage.AssetId);
        }

        return authServicesForAsset;
    }

    private async Task<List<Size>> GetSizes(OrchestrationImage orchestrationImage)
    {
        try
        {
            var thumbs = await thumbRepository.GetAllSizes(orchestrationImage.AssetId);

            if (thumbs.IsNullOrEmpty())
            {
                Logger.LogInformation("No thumbnails found for {Asset}", orchestrationImage.AssetId);
                return Enumerable.Empty<Size>().ToList();
            }
            
            return thumbs.Select(s => Size.FromArray(s)).ToList();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting size for info.json for {Asset}", orchestrationImage.AssetId);
            return Enumerable.Empty<Size>().ToList();
        }
    }
    
    protected static List<Tile> GetTiles(OrchestrationImage orchestrationImage)
    {
        // This code is working out the max tiles size based on max unauthorised.
        // The tile size must be a power of 2 and less than maxUnauthorised
        // for example, if maxUnauthorised is 500, the tile size will be updated to 256
        var tileSize =
            Math.Pow(2, (int)Math.Log2(orchestrationImage.MaxUnauthorised)); // Casting as it truncates

        var tiles = InfoJsonBuilder.GetTiles(orchestrationImage.Width, orchestrationImage.Height,
            (int)tileSize);
        return tiles;
    }
}

public interface IInfoJsonConstructor
{
    public Task<JsonLdBase?> BuildInfoJsonFromImageServer(OrchestrationImage orchestrationImage,
        CancellationToken cancellationToken = default);
}