using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Collections;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.IIIF;
using DLCS.Model.PathElements;
using DLCS.Web.Requests.AssetDelivery;
using DLCS.Web.Response;
using IIIF;
using IIIF.Auth.V2;
using IIIF.ImageApi.V2;
using IIIF.ImageApi.V3;
using Microsoft.Extensions.Options;
using Orchestrator.Settings;

namespace Orchestrator.Infrastructure.IIIF.Manifests;

/// <summary>
/// Class containing common methods for building different version Manifests 
/// </summary>
public abstract class IIIFManifestBuilderBase
{
    protected readonly IAssetPathGenerator AssetPathGenerator;
    protected readonly OrchestratorSettings OrchestratorSettings;
    protected readonly IThumbSizeProvider ThumbSizeProvider;
    protected const string MetadataLanguage = "none";

    public IIIFManifestBuilderBase(
        IAssetPathGenerator assetPathGenerator,
        IOptions<OrchestratorSettings> orchestratorSettings,
        IThumbSizeProvider thumbSizeProvider)
    {
        AssetPathGenerator = assetPathGenerator;
        ThumbSizeProvider = thumbSizeProvider;
        OrchestratorSettings = orchestratorSettings.Value;
    }

    protected async Task<ImageSizeDetails> RetrieveThumbnails(Asset asset, CancellationToken cancellationToken)
    {
        var allThumbSizes = await ThumbSizeProvider.GetThumbSizesForImage(asset, cancellationToken);
        var openThumbnails = allThumbSizes.Open.Select(Size.FromArray).ToList();

        var maxDerivativeSize = openThumbnails.IsNullOrEmpty()
            ? Size.Confine(OrchestratorSettings.TargetThumbnailSize, new Size(asset.Width ?? 0, asset.Height ?? 0))
            : openThumbnails.MaxBy(s => s.MaxDimension)!;

        return new ImageSizeDetails(openThumbnails, maxDerivativeSize);
    }

    protected List<IService> GetImageServiceForThumbnail(Asset asset, CustomerPathElement customerPathElement,
        bool forPresentation2, List<Size> thumbnailSizes)
    {
        var services = new List<IService>();
        if (OrchestratorSettings.ImageServerConfig.VersionPathTemplates.ContainsKey(global::IIIF.ImageApi.Version.V2))
        {
            var imageService = new ImageService2
            {
                Id = GetFullyQualifiedId(asset, customerPathElement, true, global::IIIF.ImageApi.Version.V2),
                Profile = ImageService2.Level0Profile,
                Sizes = thumbnailSizes,
                Context = ImageService2.Image2Context,
            };

            if (forPresentation2) imageService.Type = null; // '@Type' is not used in Presentation2

            services.Add(imageService);
        }

        // NOTE - we never include ImageService3 on Presentation2 manifests
        if (forPresentation2) return services;

        if (OrchestratorSettings.ImageServerConfig.VersionPathTemplates.ContainsKey(global::IIIF.ImageApi.Version.V3))
        {
            services.Add(new ImageService3
            {
                Id = GetFullyQualifiedId(asset, customerPathElement, true, global::IIIF.ImageApi.Version.V3),
                Profile = ImageService3.Level0Profile,
                Sizes = thumbnailSizes,
                Context = ImageService3.Image3Context,
            });
        }

        return services;
    }

    protected string GetFullQualifiedThumbPath(Asset asset, CustomerPathElement customerPathElement,
        List<Size> availableThumbs)
    {
        var targetThumb = OrchestratorSettings.TargetThumbnailSize;

        // Get the thumbnail size that is closest to the system-wide TargetThumbnailSize
        var closestSize = availableThumbs.SizeClosestTo(targetThumb);

        return GetFullQualifiedImagePath(asset, customerPathElement, closestSize, true);
    }

    protected string GetFullQualifiedImagePath(Asset asset, CustomerPathElement customerPathElement, Size size,
        bool isThumb)
    {
        var request = new BasicPathElements
        {
            Space = asset.Space,
            AssetPath = $"{asset.Id.Asset}/full/{size.Width},{size.Height}/0/default.jpg",
            RoutePrefix = isThumb ? OrchestratorSettings.Proxy.ThumbsPath : OrchestratorSettings.Proxy.ImagePath,
            CustomerPathValue = customerPathElement.Id.ToString(),
        };
        return AssetPathGenerator.GetFullPathForRequest(request, true, false);
    }

    protected string GetCanvasId(Asset asset, CustomerPathElement customerPathElement, int index)
    {
        var fullyQualifiedImageId = GetFullyQualifiedId(asset, customerPathElement, false);
        return string.Concat(fullyQualifiedImageId, "/canvas/c/", index);
    }

    protected string GetFullyQualifiedId(Asset asset, CustomerPathElement customerPathElement,
        bool isThumb, global::IIIF.ImageApi.Version imageApiVersion = global::IIIF.ImageApi.Version.Unknown)
    {
        var versionPart= imageApiVersion == OrchestratorSettings.DefaultIIIFImageVersion ||
                          imageApiVersion == global::IIIF.ImageApi.Version.Unknown
            ? string.Empty
            : $"/{imageApiVersion.ToString().ToLower()}";

        var routePrefix= isThumb
            ? OrchestratorSettings.Proxy.ThumbsPath
            : OrchestratorSettings.Proxy.ImagePath;

        var imageRequest = new BasicPathElements
        {
            Space = asset.Space,
            AssetPath = asset.Id.Asset,
            VersionPathValue = versionPart,
            RoutePrefix = routePrefix,
            CustomerPathValue = customerPathElement.Id.ToString(),
        };
        return AssetPathGenerator.GetFullPathForRequest(imageRequest, true, false);
    }

    protected List<IService> GetImageServices(Asset asset, CustomerPathElement customerPathElement, bool forPresentation2,
        List<IService>? authServices)
    {
        var versionPathTemplates = OrchestratorSettings.ImageServerConfig.VersionPathTemplates;

        var services = new List<IService>();
        if (versionPathTemplates.ContainsKey(global::IIIF.ImageApi.Version.V2))
        {
            var imageService = new ImageService2
            {
                Id = GetFullyQualifiedId(asset, customerPathElement, false, global::IIIF.ImageApi.Version.V2),
                Profile = ImageService2.Level2Profile,
                Context = ImageService2.Image2Context,
                Width = asset.Width ?? 0,
                Height = asset.Height ?? 0,
                Service = authServices,
            };

            // '@Type' is not used in Presentation2 embedded
            if (forPresentation2) imageService.Type = null; 

            services.Add(imageService);
        }

        // NOTE - we never include ImageService3 on Presentation2 manifests
        if (forPresentation2) return services;

        if (versionPathTemplates.ContainsKey(global::IIIF.ImageApi.Version.V3))
        {
            services.Add(new ImageService3
            {
                Id = GetFullyQualifiedId(asset, customerPathElement, false, global::IIIF.ImageApi.Version.V3),
                Profile = ImageService3.Level2Profile,
                Context = ImageService3.Image3Context,
                Width = asset.Width ?? 0,
                Height = asset.Height ?? 0,
                Service = authServices,
            });
        }

        // AuthServices are included on both the ImageService and the "Image" body. This allows viewers to see the
        // static image requires auth, as well as the ImageService(s) 
        if (!authServices.IsNullOrEmpty())
        {
            services.AddRange(authServices);
        }

        return services;
    }

    protected static Dictionary<string, string> GetCanvasMetadata(Asset asset) =>
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
    
    protected static Dictionary<string, string> GetManifestMetadata() =>
        new()
        {
            ["Title"] = "Created by DLCS",
            ["Generated On"] = DateTime.UtcNow.ToString("u"),
        };

    protected static bool ShouldAddThumbs(Asset asset, ImageSizeDetails thumbnailSizes) =>
        asset.HasDeliveryChannel(AssetDeliveryChannels.Thumbnails) && !thumbnailSizes.OpenThumbnails.IsNullOrEmpty();

    /// <summary>
    /// Class containing details of available thumbnail sizes
    /// </summary>
    protected class ImageSizeDetails
    {
        public ImageSizeDetails(List<Size> openThumbnails, Size maxDerivativeSize)
        {
            OpenThumbnails = openThumbnails;
            MaxDerivativeSize = maxDerivativeSize;
        }

        /// <summary>
        /// List of open available thumbnails
        /// </summary>
        public List<Size> OpenThumbnails { get; }

        /// <summary>
        /// The size of the largest derivative, according to thumbnail policy.
        /// </summary>
        public Size MaxDerivativeSize { get; }
    }
}
