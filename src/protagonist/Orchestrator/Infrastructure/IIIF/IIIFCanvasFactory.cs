﻿using System.Collections.Generic;
using System.Linq;
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
using IIIF.Presentation.V2.Annotation;
using IIIF.Presentation.V2.Strings;
using IIIF.Presentation.V3.Annotation;
using IIIF.Presentation.V3.Content;
using IIIF.Presentation.V3.Strings;
using Microsoft.Extensions.Options;
using Orchestrator.Settings;
using ImageApi = IIIF.ImageApi;
using IIIF2 = IIIF.Presentation.V2;
using IIIF3 = IIIF.Presentation.V3;

namespace Orchestrator.Infrastructure.IIIF;

/// <summary>
/// Canvas factory for creating Canvases from Assets items for IIIF Manifests. 
/// </summary>
public class IIIFCanvasFactory
{
    private readonly IAssetPathGenerator assetPathGenerator;
    private readonly OrchestratorSettings orchestratorSettings;
    private readonly IThumbSizeProvider thumbSizeProvider;
    private const string MetadataLanguage = "none";
    
    public IIIFCanvasFactory(
        IAssetPathGenerator assetPathGenerator,
        IOptions<OrchestratorSettings> orchestratorSettings,
        IThumbSizeProvider thumbSizeProvider)
    {
        this.assetPathGenerator = assetPathGenerator;
        this.thumbSizeProvider = thumbSizeProvider;
        this.orchestratorSettings = orchestratorSettings.Value;
    }

    /// <summary>
    /// Generate IIIF V3 canvases for assets.
    /// </summary>
    public async Task<List<IIIF3.Canvas>> CreateV3Canvases(List<Asset> results,
        CustomerPathElement customerPathElement, Dictionary<AssetId, AuthProbeService2>? authProbeServices)
    {
        int counter = 0;
        var canvases = new List<IIIF3.Canvas>(results.Count);
        foreach (var asset in results)
        {
            var fullyQualifiedImageId = GetFullyQualifiedId(asset, customerPathElement, false);
            var canvasId = string.Concat(fullyQualifiedImageId, "/canvas/c/", ++counter);
            var thumbnailSizes = await RetrieveThumbnails(asset);

            var canvas = new IIIF3.Canvas
            {
                Id = canvasId,
                Label = new LanguageMap("en", $"Canvas {counter}"),
                Width = asset.Width,
                Height = asset.Height,
                Metadata = GetImageMetadata(asset)
                    .Select(m => 
                        new LabelValuePair(new LanguageMap(MetadataLanguage, m.Key), 
                            new LanguageMap(MetadataLanguage, m.Value)))
                    .ToList(),
                Items = new AnnotationPage
                {
                    Id = $"{canvasId}/page",
                    Items = new PaintingAnnotation
                    {
                        Target = new IIIF3.Canvas { Id = canvasId },
                        Id = $"{canvasId}/page/image",
                        Body = asset.HasDeliveryChannel(AssetDeliveryChannels.Image)
                            ? new Image
                                {
                                    Id = GetFullQualifiedImagePath(asset, customerPathElement,
                                        thumbnailSizes.MaxDerivativeSize, false),
                                    Format = "image/jpeg",
                                    Width = thumbnailSizes.MaxDerivativeSize.Width,
                                    Height = thumbnailSizes.MaxDerivativeSize.Height,
                                    Service = GetImageServices(asset, customerPathElement, false, authProbeServices)
                                }
                            : null,
                    }.AsListOf<IAnnotation>()
                }.AsList()
            };

            if (ShouldAddThumbs(asset, thumbnailSizes))
            {
                canvas.Thumbnail = new IIIF3.Content.Image
                {
                    Id = GetFullQualifiedThumbPath(asset, customerPathElement, thumbnailSizes.OpenThumbnails),
                    Format = "image/jpeg",
                    Service = GetImageServiceForThumbnail(asset, customerPathElement, false,
                        thumbnailSizes.OpenThumbnails)
                }.AsListOf<ExternalResource>();
            }

            canvases.Add(canvas);
        }

        return canvases;
    }

    /// <summary>
    /// Generate IIIF V2 canvases for assets.
    /// </summary>
    public async Task<List<IIIF2.Canvas>> CreateV2Canvases(List<Asset> results,
        CustomerPathElement customerPathElement)
    {
        int counter = 0;
        var canvases = new List<IIIF2.Canvas>(results.Count);
        foreach (var asset in results)
        {
            var fullyQualifiedImageId = GetFullyQualifiedId(asset, customerPathElement, false);
            var canvasId = string.Concat(fullyQualifiedImageId, "/canvas/c/", ++counter);
            var thumbnailSizes = await RetrieveThumbnails(asset);

            var canvas = new IIIF2.Canvas
            {
                Id = canvasId,
                Label = new MetaDataValue($"Canvas {counter}"),
                Width = asset.Width,
                Height = asset.Height,
                Metadata = GetImageMetadata(asset)
                    .Select(m => new IIIF2.Metadata()
                    {
                        Label = new MetaDataValue(m.Key),
                        Value = new MetaDataValue(m.Value)
                    })
                    .ToList(),
                Images = new ImageAnnotation
                {
                    Id = string.Concat(fullyQualifiedImageId, "/imageanno/0"),
                    On = canvasId,
                    Resource = asset.HasDeliveryChannel(AssetDeliveryChannels.Image) 
                        ? new IIIF2.ImageResource
                            {
                                Id = GetFullQualifiedImagePath(asset, customerPathElement,
                                    thumbnailSizes.MaxDerivativeSize, false),
                                Width = thumbnailSizes.MaxDerivativeSize.Width,
                                Height = thumbnailSizes.MaxDerivativeSize.Height,
                                Service = GetImageServices(asset, customerPathElement, true, null)
                            }
                        : null,
                }.AsList()
            };

            if (ShouldAddThumbs(asset, thumbnailSizes))
            {
                canvas.Thumbnail = new IIIF2.Thumbnail
                {
                    Id = GetFullQualifiedThumbPath(asset, customerPathElement, thumbnailSizes.OpenThumbnails),
                    Service = GetImageServiceForThumbnail(asset, customerPathElement, true,
                        thumbnailSizes.OpenThumbnails)
                }.AsList();
            }

            canvases.Add(canvas);
        }

        return canvases;
    }
    
    private async Task<ImageSizeDetails?> RetrieveThumbnails(Asset asset)
    {
        var allThumbSizes = await thumbSizeProvider.GetThumbSizesForImage(asset);
        var openThumbnails = allThumbSizes.Open.Select(Size.FromArray).ToList();

        var maxDerivativeSize = openThumbnails.IsNullOrEmpty()
            ? Size.Confine(orchestratorSettings.TargetThumbnailSize, new Size(asset.Width ?? 0, asset.Height ?? 0))
            : openThumbnails.MaxBy(s => s.MaxDimension)!;
        
        return new ImageSizeDetails
        {
            MaxDerivativeSize = maxDerivativeSize,
            OpenThumbnails = openThumbnails,
        };
    }
    
    private List<IService> GetImageServiceForThumbnail(Asset asset, CustomerPathElement customerPathElement, 
        bool forPresentation2, List<Size> thumbnailSizes)
    {
        var services = new List<IService>();
        if (orchestratorSettings.ImageServerConfig.VersionPathTemplates.ContainsKey(ImageApi.Version.V2))
        {
            var imageService = new ImageService2
            {
                Id = GetFullyQualifiedId(asset, customerPathElement, true, ImageApi.Version.V2),
                Profile = ImageService2.Level0Profile,
                Sizes = thumbnailSizes,
                Context = ImageService2.Image2Context,
            };
            
            if (forPresentation2) imageService.Type = null; // '@Type' is not used in Presentation2

            services.Add(imageService);
        }

        // NOTE - we never include ImageService3 on Presentation2 manifests
        if (forPresentation2) return services;

        if (orchestratorSettings.ImageServerConfig.VersionPathTemplates.ContainsKey(ImageApi.Version.V3))
        {
            services.Add(new ImageService3
            {
                Id = GetFullyQualifiedId(asset, customerPathElement, true, ImageApi.Version.V3),
                Profile = ImageService3.Level0Profile,
                Sizes = thumbnailSizes,
                Context = ImageService3.Image3Context,
            });
        }

        return services;
    }

    private string GetFullQualifiedThumbPath(Asset asset, CustomerPathElement customerPathElement,
        List<Size> availableThumbs)
    {
        var targetThumb = orchestratorSettings.TargetThumbnailSize;

        // Get the thumbnail size that is closest to the system-wide TargetThumbnailSize
        var closestSize = availableThumbs.SizeClosestTo(targetThumb);

        return GetFullQualifiedImagePath(asset, customerPathElement, closestSize, true);
    }

    private string GetFullQualifiedImagePath(Asset asset, CustomerPathElement customerPathElement, Size size,
        bool isThumb)
    {
        var request = new BasicPathElements
        {
            Space = asset.Space,
            AssetPath = $"{asset.Id.Asset}/full/{size.Width},{size.Height}/0/default.jpg",
            RoutePrefix = isThumb ? orchestratorSettings.Proxy.ThumbsPath : orchestratorSettings.Proxy.ImagePath,
            CustomerPathValue = customerPathElement.Id.ToString(),
        };
        return assetPathGenerator.GetFullPathForRequest(request, true, false);
    }

    private string GetFullyQualifiedId(Asset asset, CustomerPathElement customerPathElement,
        bool isThumb, ImageApi.Version imageApiVersion = ImageApi.Version.Unknown)
    {
        var versionPart = imageApiVersion == orchestratorSettings.DefaultIIIFImageVersion ||
                          imageApiVersion == ImageApi.Version.Unknown
            ? string.Empty
            : $"/{imageApiVersion.ToString().ToLower()}";

        var routePrefix = isThumb
            ? orchestratorSettings.Proxy.ThumbsPath
            : orchestratorSettings.Proxy.ImagePath;
        
        var imageRequest = new BasicPathElements
        {
            Space = asset.Space,
            AssetPath = asset.Id.Asset,
            VersionPathValue = versionPart,
            RoutePrefix = routePrefix,
            CustomerPathValue = customerPathElement.Id.ToString(),
        };
        return assetPathGenerator.GetFullPathForRequest(imageRequest, true, false);
    }

    private List<IService> GetImageServices(Asset asset, CustomerPathElement customerPathElement, bool forPresentation2,
        Dictionary<AssetId, AuthProbeService2>? authProbeServices)
    {
        var noAuthServices = authProbeServices.IsNullOrEmpty();
        var versionPathTemplates = orchestratorSettings.ImageServerConfig.VersionPathTemplates;

        var services = new List<IService>();
        if (versionPathTemplates.ContainsKey(ImageApi.Version.V2))
        {
            var imageService = new ImageService2
            {
                Id = GetFullyQualifiedId(asset, customerPathElement, false, ImageApi.Version.V2),
                Profile = ImageService2.Level2Profile,
                Context = ImageService2.Image2Context,
                Width = asset.Width ?? 0,
                Height = asset.Height ?? 0,
                Service = TryGetAuthServices(),
            };

            if (forPresentation2) imageService.Type = null; // '@Type' is not used in Presentation2
            
            services.Add(imageService);
        }

        // NOTE - we never include ImageService3 on Presentation2 manifests
        if (forPresentation2) return services;
        
        if (versionPathTemplates.ContainsKey(ImageApi.Version.V3))
        {
            services.Add(new ImageService3
            {
                Id = GetFullyQualifiedId(asset, customerPathElement, false, ImageApi.Version.V3),
                Profile = ImageService3.Level2Profile,
                Context = ImageService3.Image3Context,
                Width = asset.Width ?? 0,
                Height = asset.Height ?? 0,
                Service = TryGetAuthServices(),
            });
        }
        
        return services;

        List<IService>? TryGetAuthServices()
        {
            if (noAuthServices) return null;
            if (!authProbeServices!.TryGetValue(asset.Id, out var probeService2)) return null;

            var authServiceToAdd = probeService2.ToEmbeddedService();
            return authServiceToAdd.AsListOf<IService>();
        }
    }
    
    private Dictionary<string, string> GetImageMetadata(Asset asset)
    {
        return new Dictionary<string, string>()
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
    }
    
    private static bool ShouldAddThumbs(Asset asset, ImageSizeDetails thumbnailSizes)
    {
        return asset.HasDeliveryChannel(AssetDeliveryChannels.Thumbnails) && !thumbnailSizes.OpenThumbnails.IsNullOrEmpty();
    }
    
    /// <summary>
    /// Class containing details of available thumbnail sizes
    /// </summary>
    private class ImageSizeDetails
    {
        /// <summary>
        /// List of open availabel thumbnails
        /// </summary>
        public List<Size> OpenThumbnails { get; set; }

        /// <summary>
        /// The size of the largest derivative, according to thumbnail policy.
        /// </summary>
        public Size MaxDerivativeSize { get; set; }
    }
}