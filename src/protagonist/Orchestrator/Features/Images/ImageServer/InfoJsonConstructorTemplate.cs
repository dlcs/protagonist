using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Collections;
using DLCS.Core.Guard;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using IIIF;
using IIIF.ImageApi;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestrator.Assets;
using Orchestrator.Infrastructure.IIIF;
using Orchestrator.Settings;

namespace Orchestrator.Features.Images.ImageServer;

/// <summary>
/// Template base class responsible for orchestrating image, calling IImageServerClient to get info.json and update with
/// required information that image-server will be unaware of (e.g. Auth, Id).
/// </summary>
public abstract class InfoJsonConstructorTemplate<T>(
    IImageServerClient imageServerClient,
    IThumbRepository thumbRepository,
    IIIIFAuthBuilder iiifAuthBuilder,
    IAssetTracker assetTracker,
    IOptions<OrchestratorSettings> orchestratorSettings,
    ILogger logger) : IInfoJsonConstructor where T : JsonLdBase
{
    /// <summary>Whether upscaling is allowed (<see cref="OrchestratorSettings.AllowUpscaling"/>).</summary>
    protected readonly bool AllowUpscaling = orchestratorSettings.Value.AllowUpscaling;

    protected abstract IIIF.ImageApi.Version ImageApiVersion { get; }
    protected readonly ILogger Logger = logger;

    public async Task<JsonLdBase?> BuildInfoJsonFromImageServer(AssetId assetId,
        CancellationToken cancellationToken = default)
    {
        var orchestrationImage = await GetRefreshedOrchestrationImage(assetId);

        // Start task to get "sizes" from thumbnails - the vast majority of cases will have set sizes
        var getSizesFromThumbnailsTask = GetSizes(orchestrationImage);

        // Get info.json from downstream image server and add dlcs-known elements (services, thumbs) to it
        var imageService =
            await imageServerClient.GetInfoJson<T>(orchestrationImage, ImageApiVersion, cancellationToken);
        if (imageService == null) return null;

        await UpdateImageService(imageService, orchestrationImage, cancellationToken);
        var sizesFromThumbnails = await getSizesFromThumbnailsTask;
        SetInfoJsonSizes(imageService, orchestrationImage, sizesFromThumbnails);

        return imageService;
    }

    private void SetInfoJsonSizes(T imageService, OrchestrationImage orchestrationImage, List<Size> sizesFromThumbnails)
    {
        if (!sizesFromThumbnails.IsNullOrEmpty())
        {
            SetImageServiceSizes(imageService, sizesFromThumbnails);
            return;
        }

        // If we didn't get any sizes from thumbnails, filter those from image-service
        logger.LogInformation("No sizes found for {AssetId}, filtering those from image-server",
            orchestrationImage.AssetId);
        var imageServerSizes = GetImageServiceSizes(imageService);
        var correctedSizes = imageServerSizes.Where(sz => sz.MaxDimension <= orchestrationImage.MaxWidth).ToList();
        SetImageServiceSizes(imageService, correctedSizes);
    }
    
    // We always want to generate info.json using a fresh OrchestrationImage. If it's stale we could set an invalid
    // property (e.g. maxWidth or roles that have very recently been updated in API)
    private async Task<OrchestrationImage> GetRefreshedOrchestrationImage(AssetId assetId)
    {
        var orchestrationImage = await assetTracker.RefreshCachedAsset<OrchestrationImage>(assetId);
        return orchestrationImage.ThrowIfNull(nameof(orchestrationImage));
    }

    private async Task UpdateImageService(T? imageService, OrchestrationImage orchestrationImage,
        CancellationToken cancellationToken)
    {
        if (imageService == null) return;

        SetImageServiceStubId(imageService, orchestrationImage);
        
        // Set the maxWidth - we will always have a value, regardless of asset specified or system default
        SetImageServiceMaxWidth(imageService, orchestrationImage);

        // Whether upscaling is advertised (v3 'sizeUpscaling'), whatever the image server's own info.json said
        SetImageServiceUpscaling(imageService);

        if (orchestrationImage.RequiresAuth)
        {
            // If the only role is the internal 'unobtainable' role, don't add auth-services as we won't advertise this
            if (orchestrationImage.Roles.ContainsOnly(Asset.UnobtainableRole))
            {
                Logger.LogDebug("Asset {AssetId} requires auth but has unobtainable roles.",
                    orchestrationImage.AssetId);
            }
            else
            {
                Logger.LogDebug("Asset {AssetId} requires auth with roles, adding auth-services",
                    orchestrationImage.AssetId);
                await SetImageServiceAuthServices(imageService, orchestrationImage, cancellationToken);
            }
        }


        TrySetImageServiceTiles(imageService, orchestrationImage);
    }

    /// <summary>
    /// Get "sizes" from image service
    /// </summary>
    protected abstract List<Size> GetImageServiceSizes(T imageService);
    
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
    /// Advertise upscaling only when it is allowed. Default: nothing - IIIF v2.1 upscaling is ill-defined, and without
    /// it the Orchestrator clamps v2 sizes to the region rather than refusing them.
    /// </summary>
    protected virtual void SetImageServiceUpscaling(T imageService)
    {
    }

    /// <summary>
    /// The maxWidth to advertise. Without upscaling (<see cref="OrchestratorSettings.AllowUpscaling"/> false) only when
    /// it is smaller than the image's larger dimension: a maxWidth above the image's size reads as an offer of larger
    /// sizes (IIIF reads a lone maxWidth as a square box), and crawlers take it - in November 2026 info.json advertising
    /// maxWidth 10000 drew /full/,10000/ requests for ordinary pages. See ADR 0011.
    /// </summary>
    protected int? GetAdvertisedMaxWidth(OrchestrationImage orchestrationImage) =>
        AllowUpscaling || orchestrationImage.Size.MaxDimension > orchestrationImage.MaxWidth
            ? orchestrationImage.MaxWidth
            : null;

    /// <summary>
    /// Set the stub Id property, this will be overwritten further downstream
    /// </summary>
    protected abstract void SetImageServiceStubId(T imageService, OrchestrationImage orchestrationImage);

    /// <summary>
    /// Overwrite the "sizes" property on info.json with given sizes
    /// </summary>
    protected abstract void SetImageServiceSizes(T imageService, List<Size> sizes);

    /// <summary>
    /// Overwrite the "tiles" property on info.json with given tile sizes if required.
    /// This will happen if there are no tiles, or the advertised tile would be smaller than maxWidth.
    /// </summary>
    /// <param name="imageService">The image service</param>
    /// <param name="orchestrationImage">The image being orchestrated</param>
    protected abstract void TrySetImageServiceTiles(T imageService, OrchestrationImage orchestrationImage);
    
    protected async Task<IService?> GetAuth2Service(OrchestrationImage orchestrationImage,
        CancellationToken cancellationToken)
    {
        var authServicesForAsset = await iiifAuthBuilder.GetAuthServices(orchestrationImage.AssetId,
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

            return thumbs.Select(Size.FromArray).ToList();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting size for info.json for {Asset}", orchestrationImage.AssetId);
            return Enumerable.Empty<Size>().ToList();
        }
    }

    /// <summary>
    /// Check whether tiles should be updated - true if there are no Tiles, or the Tiles size exceeds the MaxWidth
    /// </summary>
    protected bool ShouldUpdateTiles(List<Tile> existingTiles, OrchestrationImage orchestrationImage) =>
        existingTiles.IsNullOrEmpty() || existingTiles.Select(s => s.Width).Max() > orchestrationImage.MaxWidth;

    protected static List<Tile> GetTiles(OrchestrationImage orchestrationImage)
    {
        // Work out the max tiles size based on maxWidth.
        // The tile size must be a power of 2 and less than maxWidth
        // for example, if maxWidth is 500, the tile size will be updated to 256
        var tileSize = Math.Pow(2, (int)Math.Log2(orchestrationImage.MaxWidth)); // Casting as it truncates

        var tiles = InfoJsonBuilder.GetTiles(orchestrationImage.Size.Width, orchestrationImage.Size.Height,
            (int)tileSize);
        return tiles;
    }
}

public interface IInfoJsonConstructor
{
    public Task<JsonLdBase?> BuildInfoJsonFromImageServer(AssetId assetId,
        CancellationToken cancellationToken = default);
}
