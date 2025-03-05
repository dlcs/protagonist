using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core;
using DLCS.Core.Collections;
using DLCS.Core.Strings;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Assets.Metadata;
using DLCS.Model.IIIF;
using DLCS.Model.PathElements;
using DLCS.Web.Requests.AssetDelivery;
using DLCS.Web.Response;
using IIIF;
using IIIF.Auth.V2;
using IIIF.Presentation;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Annotation;
using IIIF.Presentation.V3.Content;
using IIIF.Presentation.V3.Strings;
using Microsoft.Extensions.Logging;
using IIIFAuth2 = IIIF.Auth.V2;

namespace Orchestrator.Infrastructure.IIIF.Manifests;

/// <summary>
/// Implementation of <see cref="IBuildManifests{T}"/> responsible for generating IIIF v3 manifest
/// </summary>
public class ManifestV3Builder : IBuildManifests<Manifest>
{
    private readonly IManifestBuilderUtils builderUtils;
    private readonly IAssetPathGenerator assetPathGenerator;
    private readonly IIIIFAuthBuilder authBuilder;
    private readonly ILogger<ManifestV3Builder> logger;
    private const string ManifestLanguage = "en";
    private const string CanvasLanguage = "none";

    public ManifestV3Builder(
        IManifestBuilderUtils builderUtils,
        IAssetPathGenerator assetPathGenerator,
        IIIIFAuthBuilder authBuilder, 
        ILogger<ManifestV3Builder> logger)
    {
        this.builderUtils = builderUtils;
        this.assetPathGenerator = assetPathGenerator;
        this.authBuilder = authBuilder;
        this.logger = logger;
    }

    public async Task<Manifest> BuildManifest(string manifestId, string label, List<Asset> assets,
        CustomerPathElement customerPathElement, ManifestType manifestType, CancellationToken cancellationToken)
    {
        var probeServices = await GetProbeServices(assets, cancellationToken);
        var anyAssetRequireAuth = !probeServices.IsNullOrEmpty();

        var manifest = new Manifest
        {
            Id = manifestId,
            Label = new LanguageMap(ManifestLanguage, label),
            Metadata = ManifestBuilderUtils.GetManifestMetadata().ToV3Metadata(ManifestLanguage),
        };

        manifest.EnsurePresentation3Context();
        if (anyAssetRequireAuth)
        {
            logger.LogTrace(
                "ManifestId {ManifestId} has at least 1 asset requiring auth - adding Auth2 context + services",
                manifestId);
            manifest.EnsureContext(IIIFAuth2.Constants.IIIFAuth2Context);

            // Add the AuthAccessServices to the manifest services collection.
            // Individual content-resources will contain ProbeService and reference this accessService
            var accessServices = GetDistinctAccessServices(probeServices);
            manifest.Services = accessServices;
        }

        await PopulateManifest(manifest, assets, customerPathElement, probeServices, cancellationToken);
        return manifest;
    }

    private async Task PopulateManifest(Manifest manifest, List<Asset> results,
        CustomerPathElement customerPathElement, Dictionary<AssetId, AuthProbeService2>? authProbeServices,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Populating manifest {ManifestId}", manifest.Id);
        var probeServices = authProbeServices ?? new Dictionary<AssetId, AuthProbeService2>();
        int counter = 0;
        var canvases = new List<Canvas>(results.Count);
        var additionalContexts = new List<string>();
        foreach (var asset in results)
        {
            var assetCanvas = await GetCanvas(asset, customerPathElement, ++counter, probeServices, cancellationToken);
            if (assetCanvas.Canvas != null) canvases.Add(assetCanvas.Canvas);

            if (!assetCanvas.AdditionalContexts.IsNullOrEmpty())
            {
                additionalContexts.AddRange(assetCanvas.AdditionalContexts);
            }
        }

        foreach (var additionalContext in additionalContexts.Distinct())
        {
            manifest.EnsureContext(additionalContext);
        }
        
        manifest.Items = canvases;
        manifest.Thumbnail = canvases.FirstOrDefault(c => !c.Thumbnail.IsNullOrEmpty())?.Thumbnail;
    }

    private async Task<AssetCanvas> GetCanvas(Asset asset, CustomerPathElement customerPathElement, int canvasIndex,
        Dictionary<AssetId, AuthProbeService2> authProbeServices, CancellationToken cancellationToken)
    {
        /*
         * If 'iiif-img'; add "Image" body on AnnotationPage>PaintingAnnotation
         * If 'thumbs'; add "Thumbnail" on canvas
         * If 'iiif-av' and single transcode; add "Sound" OR "Video" body on AnnotationPage>PaintingAnnotation
         * If 'iiif-av' and multi transcode; add "Choice" body on AnnotationPage>PaintingAnnotation
         * If 'iiif-av' and no AppMetadata then no Canvas
         * If 'file' only; add weco BD style placeholder body (incl custom behaviours + context)
         * If 'file'; add "Rendering" on canvas (always, canvas painted resource taken care of with one of above)
         *
         * Other notes:
         *  Dimensions on Canvas can differ depending on type (temporal and/or spatial)
         *  Metadata is same
         *  CanvasId isn't same (do we want to change this??)
         *
         * Auth services - on "body" always. Also on ImageService for images
         */

        // The basic properties of a canvas are identical regardless of how the asset is available
        var canvas = new Canvas
        {
            Id = builderUtils.GetCanvasId(asset, customerPathElement, canvasIndex),
            Label = new LanguageMap("en", $"Canvas {canvasIndex}"),
            Metadata = ManifestBuilderUtils.GetCanvasMetadata(asset).ToV3Metadata(CanvasLanguage),
        };
        
        var assetCanvas =
            await GetCanvasForAsset(asset, customerPathElement, canvas, authProbeServices, cancellationToken);
        
        return assetCanvas;
    }

    private async Task<AssetCanvas> GetCanvasForAsset(Asset asset, CustomerPathElement customerPathElement, Canvas canvas,
        Dictionary<AssetId, AuthProbeService2> authProbeServices, CancellationToken cancellationToken)
    {
        var authServices = GetAuthServices(asset, authProbeServices);
        
        logger.LogTrace("Adding canvas {CanvasId} to manifest", canvas.Id);
        
        AnnotationPage? annotationPage = null;
        ExternalResource? thumbnail = null;
        var additionalContexts = new List<string>();
        
        if (asset.HasAnyDeliveryChannel(AssetDeliveryChannels.Image, AssetDeliveryChannels.Thumbnails))
        {
            // If Image or Thumbnail then it will have Image body and/or thumbnail
            (annotationPage, thumbnail) =
                await HandleImageAsset(asset, customerPathElement, canvas, authServices, cancellationToken);
        }
        else if (asset.HasDeliveryChannel(AssetDeliveryChannels.Timebased))
        {
            // If Timebased then it will only have an annotation body (no thumbs)
            annotationPage = HandleTimebasedAsset(asset, customerPathElement, canvas, authServices);
        }

        if (asset.HasDeliveryChannel(AssetDeliveryChannels.File))
        {
            var isFileOnly = false;
            
            // If asset _only_ has file delivery channel then add a placeholder canvas
            if (asset.HasSingleDeliveryChannel(AssetDeliveryChannels.File))
            {
                additionalContexts.Add(BornDigitalConsts.Context);
                isFileOnly = true;
                annotationPage = GetFileChannelPlaceholder(asset, canvas);
            }

            // Safety - prevents us trying to add a rendering when there is no choice
            if (annotationPage != null)
            {
                var fileRendering = GetRenderingForAsset(asset, customerPathElement);
                if (isFileOnly)
                {
                    // 'file' channel is only one so use the 'original' behaviour as per born-digital RFC
                    fileRendering.Behavior ??= new List<string>();
                    fileRendering.Behavior.Add(BornDigitalConsts.OriginalBehavior);
                }

                canvas.Rendering = fileRendering.AsList();
            }
        }

        if (annotationPage == null)
        {
            return new AssetCanvas(null, null);
        }
        
        canvas.Items = annotationPage.AsList();

        if (thumbnail != null)
        {
            canvas.Thumbnail = thumbnail.AsListOf<ExternalResource>();
        }
        
        return new AssetCanvas(canvas, additionalContexts);
    }

    private async Task<(AnnotationPage annotationPage, ExternalResource? thumbnail)> HandleImageAsset(Asset asset,
        CustomerPathElement customerPathElement, Canvas canvas, List<IService>? authServices, 
        CancellationToken cancellationToken)
    {
        logger.LogTrace("{CanvasId} is image, processing", canvas.Id);
        var thumbnailSizes = await builderUtils.RetrieveThumbnails(asset, cancellationToken);

        var canvasId = canvas.Id;
        canvas.Width = asset.Width;
        canvas.Height = asset.Height;

        var annotationPage = new AnnotationPage
        {
            Id = $"{canvasId}/page",
            Items = new PaintingAnnotation
            {
                Target = new Canvas { Id = canvasId },
                Id = $"{canvasId}/page/image",
                Body = new Image
                {
                    Id = builderUtils.GetFullQualifiedImagePath(asset, customerPathElement,
                        thumbnailSizes.MaxDerivativeSize, false),
                    Format = "image/jpeg",
                    Width = thumbnailSizes.MaxDerivativeSize.Width,
                    Height = thumbnailSizes.MaxDerivativeSize.Height,
                    Service = asset.HasDeliveryChannel(AssetDeliveryChannels.Image)
                        ? builderUtils.GetImageServices(asset, customerPathElement, false, authServices)
                        : null,
                },
            }.AsListOf<IAnnotation>(),
        };

        if (!builderUtils.ShouldAddThumbs(asset, thumbnailSizes)) return (annotationPage, null);

        var thumbnail = new Image
        {
            Id = builderUtils.GetFullQualifiedThumbPath(asset, customerPathElement, thumbnailSizes.OpenThumbnails),
            Format = "image/jpeg",
            Service = builderUtils.GetImageServiceForThumbnail(asset, customerPathElement, false,
                thumbnailSizes.OpenThumbnails)
        };
        return (annotationPage, thumbnail);
    }
    
    private AnnotationPage? HandleTimebasedAsset(Asset asset, CustomerPathElement customerPathElement, Canvas canvas,
        List<IService>? authServices)
    {
        logger.LogTrace("{CanvasId} is timebased, processing", canvas.Id);
        var canvasId = canvas.Id;
        var transcodes = asset.AssetApplicationMetadata.GetTranscodeMetadata(false);
            
        canvas.Width = asset.Width;
        canvas.Height = asset.Height;
        canvas.Duration = asset.Duration;

        if (transcodes.IsNullOrEmpty())
        {
            logger.LogDebug("No transcode metadata found for {AssetId}, no manifest canvas will be rendered", asset.Id);
            return null;
        }

        var paintables = transcodes
            .Select(t => GetPaintableForTranscode(asset, customerPathElement, t, authServices))
            .ToList();
                
        var paintingAnnotation = new PaintingAnnotation
        {
            Target = new Canvas { Id = canvasId },
            Id = $"{canvasId}/page/image",
        };

        // For single transcode, add Audio/Sound body. Else add a choice
        paintingAnnotation.Body = transcodes.Length == 1
            ? paintables.Single()
            : new PaintingChoice
            {
                Items = paintables
            };
                
        return new AnnotationPage
        {
            Id = $"{canvasId}/page",
            Items = paintingAnnotation.AsListOf<IAnnotation>(),
        };
    }
    
    private AnnotationPage GetFileChannelPlaceholder(Asset asset, Canvas canvas)
    {
        logger.LogTrace("{CanvasId} is file only, processing", canvas.Id);
        
        var canvasId = canvas.Id!;
        canvas.Width = 1000;
        canvas.Height = 1000;
        canvas.Behavior ??= new List<string>();
        canvas.Behavior.Add(BornDigitalConsts.PlaceholderBehavior);
        
        var annotationPage = new AnnotationPage
        {
            Id = $"{canvasId}/page",
            Items = new PaintingAnnotation
            {
                Target = new Canvas { Id = canvasId },
                Id = $"{canvasId}/page/image",
                Body = new Image
                {
                    Id = GetPlaceholderUri(),
                    Width = canvas.Width,
                    Height = canvas.Height,
                    Format = "image/png",
                }
            }.AsListOf<IAnnotation>()
        };
        return annotationPage;

        string GetPlaceholderUri()
        {
            // Orchestrator serves a placeholder PNG for each type of image - build that URI here
            var rdfType = MIMEHelper.GetRdfType(asset.MediaType).ToLower();
            var b = new UriBuilder(canvasId){ Path = $"static/{rdfType}/placeholder.png"};
            return b.Uri.ToString();
        }
    }

    private ExternalResource GetRenderingForAsset(Asset asset, CustomerPathElement customerPathElement)
    {
        logger.LogTrace("Generating 'rendering' for {AssetId}", asset.Id);
        var renderingId = GetFilePath(asset, customerPathElement);
        if (MIMEHelper.IsImage(asset.MediaType))
        {
            return new Image
            {
                Id = renderingId,
                Format = asset.MediaType,
                Width = asset.Width,
                Height = asset.Height,
            };
        }

        if (MIMEHelper.IsVideo(asset.MediaType))
        {
            return new Video
            {
                Id = renderingId,
                Format = asset.MediaType,
                Width = asset.Width,
                Height = asset.Height,
                Duration = asset.Duration,
            };
        }
        
        if (MIMEHelper.IsAudio(asset.MediaType))
        {
            return new Sound
            {
                Id = renderingId,
                Format = asset.MediaType,
                Duration = asset.Duration,
            };
        }

        var rdfType = MIMEHelper.GetRdfType(asset.MediaType);
        return new ExternalResource(rdfType)
        {
            Id = renderingId,
            Format = asset.MediaType,
        };
    }

    private record AssetCanvas(Canvas? Canvas, IList<string>? AdditionalContexts);
    
    private static List<IService>? GetAuthServices(Asset asset, Dictionary<AssetId, AuthProbeService2> authProbeServices)
    {
        if (authProbeServices.IsNullOrEmpty()) return null;
        if (!authProbeServices.TryGetValue(asset.Id, out var probeService2)) return null;

        var authServiceToAdd = probeService2.ToEmbeddedService();
        return authServiceToAdd.AsListOf<IService>();
    }

    private IPaintable GetPaintableForTranscode(Asset asset, CustomerPathElement customerPathElement,
        AVTranscode transcode, List<IService>? authServices) =>
        MIMEHelper.IsVideo(transcode.MediaType)
            ? new Video
            {
                Id = GetPathForTranscode(asset, customerPathElement, transcode),
                Format = transcode.MediaType,
                Duration = transcode.Duration,
                Height = transcode.Height,
                Width = transcode.Width,
                Service = authServices,
            }
            : new Sound
            {
                Id = GetPathForTranscode(asset, customerPathElement, transcode),
                Format = transcode.MediaType,
                Duration = transcode.Duration,
                Service = authServices,
            };

    private string GetPathForTranscode(Asset asset, CustomerPathElement customerPathElement,
        AVTranscode avTranscode)
    {
        var imageRequest = new BasicPathElements
        {
            Space = asset.Space,
            AssetPath = asset.Id.Asset.ToConcatenated('/', avTranscode.GetTranscodeRequestPath()),
            RoutePrefix = AssetDeliveryChannels.Timebased,
            CustomerPathValue = customerPathElement.Id.ToString(),
        };
        return assetPathGenerator.GetFullPathForRequest(imageRequest, true, false);
    }
    
    private string GetFilePath(Asset asset, CustomerPathElement customerPathElement)
    {
        var imageRequest = new BasicPathElements
        {
            Space = asset.Space,
            AssetPath = asset.Id.Asset,
            RoutePrefix = AssetDeliveryChannels.File,
            CustomerPathValue = customerPathElement.Id.ToString(),
        };
        return assetPathGenerator.GetFullPathForRequest(imageRequest, true, false);
    }
    
    private async Task<Dictionary<AssetId, AuthProbeService2>?> GetProbeServices(IReadOnlyCollection<Asset> assets,
        CancellationToken cancellationToken)
    {
        var assetsRequiringAuth = assets.Where(a => a.RequiresAuth && !string.IsNullOrEmpty(a.Roles)).ToList();

        var assetsRequiringAuthCount = assetsRequiringAuth.Count;
        if (assetsRequiringAuthCount == 0) return null;

        var logLevel = assetsRequiringAuthCount > 10 ? LogLevel.Information : LogLevel.Debug;
        logger.Log(logLevel, "Getting Auth services for {AuthAssetCount} assets", assetsRequiringAuthCount);

        // This is doing a lot - batch the requests up?
        var sw = Stopwatch.StartNew();
        var probeServices = new Dictionary<AssetId, AuthProbeService2>(assetsRequiringAuthCount);
        var taskList = new List<Task>(assetsRequiringAuthCount);
        foreach (var asset in assetsRequiringAuth)
        {
            taskList.Add(authBuilder.GetAuthServicesForAsset(asset.Id, asset.RolesList.ToList(), cancellationToken)
                .ContinueWith(antecedent =>
                    {
                        if (antecedent.Result is AuthProbeService2 probeService2)
                            probeServices[asset.Id] = probeService2;
                    },
                    TaskContinuationOptions.OnlyOnRanToCompletion));
        }

        await Task.WhenAll(taskList);
        sw.Stop();
        logger.Log(logLevel, "Got Auth services for {AuthAssetCount} assets in {Elapsed}ms", assetsRequiringAuthCount,
            sw.ElapsedMilliseconds);

        return probeServices;
    }

    private static List<IService> GetDistinctAccessServices(Dictionary<AssetId, AuthProbeService2>? probeServices)
    {
        // Get a list of all _distinct_ access services - these are embedded at Manifest level
        // Canvases will contain references
        var accessServices = probeServices!
            .SelectMany(kvp => kvp.Value.Service?.OfType<AuthAccessService2>() ?? Array.Empty<AuthAccessService2>())
            .DistinctBy(accessService => accessService.Id)
            .Cast<IService>()
            .ToList();
        return accessServices;
    }
}
