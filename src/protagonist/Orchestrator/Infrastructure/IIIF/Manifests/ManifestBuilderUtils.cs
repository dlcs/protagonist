using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Collections;
using DLCS.Model.Assets;
using DLCS.Model.IIIF;
using DLCS.Model.PathElements;
using DLCS.Web.Requests.AssetDelivery;
using DLCS.Web.Response;
using IIIF;
using IIIF.ImageApi.V2;
using IIIF.ImageApi.V3;
using Microsoft.Extensions.Options;
using Orchestrator.Settings;

namespace Orchestrator.Infrastructure.IIIF.Manifests;

public interface IManifestBuilderUtils
{
    /// <summary>
    /// Feature flag can control whether to rewrite asset-paths, in accordance to pathTemplate, or use native paths
    /// </summary>
    bool UseNativeFormatForAssets { get; }
    
    Task<ImageSizeDetails> RetrieveThumbnails(Asset asset, CancellationToken cancellationToken);

    List<IService> GetImageServiceForThumbnail(Asset asset, CustomerPathElement customerPathElement,
        bool forPresentation2, List<Size> thumbnailSizes);

    string GetFullQualifiedThumbPath(Asset asset, CustomerPathElement customerPathElement,
        List<Size> availableThumbs);

    string GetFullQualifiedImagePath(Asset asset, CustomerPathElement customerPathElement, Size size,
        bool isThumb);

    string GetCanvasId(Asset asset, CustomerPathElement customerPathElement, int index);

    List<IService> GetImageServices(Asset asset, CustomerPathElement customerPathElement, bool forPresentation2,
        List<IService>? authServices);
    
    bool ShouldAddThumbs(Asset asset, ImageSizeDetails thumbnailSizes);
}

/// <summary>
/// Class containing common methods for building different version Manifests 
/// </summary>
public class ManifestBuilderUtils(
    IAssetPathGenerator assetPathGenerator,
    IOptions<OrchestratorSettings> orchestratorSettings,
    IThumbSizeProvider thumbSizeProvider)
    : IManifestBuilderUtils
{
    private readonly OrchestratorSettings orchestratorSettings = orchestratorSettings.Value;
    
    public bool UseNativeFormatForAssets { get; } = !orchestratorSettings.Value.RewriteAssetPathsOnManifests;

    public async Task<ImageSizeDetails> RetrieveThumbnails(Asset asset, CancellationToken cancellationToken)
    {
        var allThumbSizes = await thumbSizeProvider.GetThumbSizesForImage(asset, cancellationToken);
        var openThumbnails = allThumbSizes.Open.Select(Size.FromArray).ToList();

        var maxDerivativeSize = openThumbnails.IsNullOrEmpty()
            ? Size.Confine(orchestratorSettings.TargetThumbnailSize, new Size(asset.Width ?? 0, asset.Height ?? 0))
            : openThumbnails.MaxBy(s => s.MaxDimension)!;

        return new ImageSizeDetails(openThumbnails, maxDerivativeSize);
    }

    public List<IService> GetImageServiceForThumbnail(Asset asset, CustomerPathElement customerPathElement,
        bool forPresentation2, List<Size> thumbnailSizes)
        => GetImageServicesInternal(
            asset,
            customerPathElement,
            forPresentation2,
            true,
            image2 => image2.Sizes = thumbnailSizes,
            image3 => image3.Sizes = thumbnailSizes);

    public string GetFullQualifiedThumbPath(Asset asset, CustomerPathElement customerPathElement,
        List<Size> availableThumbs)
    {
        var targetThumb = orchestratorSettings.TargetThumbnailSize;

        // Get the thumbnail size that is closest to the system-wide TargetThumbnailSize
        var closestSize = availableThumbs.SizeClosestTo(targetThumb);

        return GetFullQualifiedImagePath(asset, customerPathElement, closestSize, true);
    }

    public string GetFullQualifiedImagePath(Asset asset, CustomerPathElement customerPathElement, Size size,
        bool isThumb)
    {
        var request = new BasicPathElements
        {
            Space = asset.Space,
            AssetPath = $"{asset.Id.Asset}/full/{size.Width},{size.Height}/0/default.jpg",
            RoutePrefix = isThumb ? AssetDeliveryChannels.Thumbnails : AssetDeliveryChannels.Image,
            CustomerPathValue = customerPathElement.Id.ToString(),
        };
        return assetPathGenerator.GetFullPathForRequest(request, UseNativeFormatForAssets, false);
    }

    public string GetCanvasId(Asset asset, CustomerPathElement customerPathElement, int index)
    {
        var fullyQualifiedImageId = GetFullyQualifiedId(asset, customerPathElement, false, true);
        return string.Concat(fullyQualifiedImageId, "/canvas/c/", index);
    }

    public List<IService> GetImageServices(Asset asset, CustomerPathElement customerPathElement, bool forPresentation2,
        List<IService>? authServices)
    {
        var services  = GetImageServicesInternal(
            asset,
            customerPathElement,
            forPresentation2,
            true,
            image2 =>
            {
                image2.Width = asset.Width ?? 0;
                image2.Height = asset.Height ?? 0;
                image2.Service = authServices;
            },
            image3 => {
                image3.Width = asset.Width ?? 0;
                image3.Height = asset.Height ?? 0;
                image3.Service = authServices;
            });
        
        // NOTE - we never include ImageService3 on Presentation2 manifests
        if (forPresentation2) return services;
        
        // AuthServices are included on both the ImageService and the "Image" body. This allows viewers to see the
        // static image requires auth, as well as the ImageService(s) 
        if (!authServices.IsNullOrEmpty())
        {
            services.AddRange(authServices);
        }

        return services;
    }

    public static Dictionary<string, string> GetCanvasMetadata(Asset asset) =>
        new()
        {
            { "String 1", asset.Reference1 ?? string.Empty },
            { "String 2", asset.Reference2 ?? string.Empty },
            { "String 3", asset.Reference3 ?? string.Empty },
            { "Number 1", (asset.NumberReference1 ?? 0).ToString() },
            { "Number 2", (asset.NumberReference2 ?? 0).ToString() },
            { "Number 3", (asset.NumberReference3 ?? 0).ToString() },
            { "Tags", asset.Tags ?? string.Empty },
            { "Roles", asset.Roles ?? string.Empty }
        };
    
    public static Dictionary<string, string> GetManifestMetadata() =>
        new()
        {
            ["Title"] = "Created by DLCS",
            ["Generated On"] = DateTime.UtcNow.ToString("u"),
        };

    public bool ShouldAddThumbs(Asset asset, ImageSizeDetails thumbnailSizes) =>
        asset.HasDeliveryChannel(AssetDeliveryChannels.Thumbnails) && !thumbnailSizes.OpenThumbnails.IsNullOrEmpty();
    
    // Helper function - has shared logic for generating ImageServices for thumbs + images while taking delegates to
    // customise
    private List<IService> GetImageServicesInternal(Asset asset, CustomerPathElement customerPathElement,
        bool forPresentation2, bool isThumb, Action<ImageService2> customiseImage2,
        Action<ImageService3> customiseImage3)
    {
        var versionPathTemplates = orchestratorSettings.ImageServerConfig.VersionPathTemplates;
        var services = new List<IService>();
        if (versionPathTemplates.ContainsKey(global::IIIF.ImageApi.Version.V2))
        {
            var image2 = new ImageService2
            {
                Id = GetFullyQualifiedId(asset, customerPathElement, isThumb, UseNativeFormatForAssets,
                    global::IIIF.ImageApi.Version.V2),
                Profile = ImageService2.Level2Profile,
                Context = ImageService2.Image2Context,
            };
            customiseImage2(image2);

            // '@Type' is not used in Presentation2 embedded
            if (forPresentation2) image2.Type = null;

            services.Add(image2);
        }

        // NOTE - we never include ImageService3 on Presentation2 manifests
        if (forPresentation2) return services;

        if (versionPathTemplates.ContainsKey(global::IIIF.ImageApi.Version.V3))
        {
            var image3 = new ImageService3
            {
                Id = GetFullyQualifiedId(asset, customerPathElement, isThumb, UseNativeFormatForAssets,
                    global::IIIF.ImageApi.Version.V3),
                Profile = ImageService3.Level2Profile,
                Context = ImageService3.Image3Context,
            };
            customiseImage3(image3);

            services.Add(image3);
        }

        return services;
    }
    
    private string GetFullyQualifiedId(Asset asset, CustomerPathElement customerPathElement,
        bool isThumb, bool useNativeFormat, global::IIIF.ImageApi.Version imageApiVersion = global::IIIF.ImageApi.Version.Unknown)
    {
        var versionPart= imageApiVersion == orchestratorSettings.DefaultIIIFImageVersion ||
                         imageApiVersion == global::IIIF.ImageApi.Version.Unknown
            ? string.Empty
            : $"/{imageApiVersion.ToString().ToLower()}";

        var routePrefix = isThumb
            ? AssetDeliveryChannels.Thumbnails
            : AssetDeliveryChannels.Image;
        
        var imageRequest = new BasicPathElements
        {
            Space = asset.Space,
            AssetPath = asset.Id.Asset,
            VersionPathValue = versionPart,
            RoutePrefix = routePrefix,
            CustomerPathValue = customerPathElement.Id.ToString(),
        };
        return assetPathGenerator.GetFullPathForRequest(imageRequest, useNativeFormat, false);
    }
}

/// <summary>
/// Class containing details of available thumbnail sizes
/// </summary>
public class ImageSizeDetails(List<Size> openThumbnails, Size maxDerivativeSize)
{
    /// <summary>
    /// List of open available thumbnails
    /// </summary>
    public List<Size> OpenThumbnails { get; } = openThumbnails;

    /// <summary>
    /// The size of the largest derivative, according to thumbnail policy.
    /// </summary>
    public Size MaxDerivativeSize { get; } = maxDerivativeSize;
}
