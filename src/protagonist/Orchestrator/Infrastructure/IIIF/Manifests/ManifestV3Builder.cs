using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Collections;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.IIIF;
using DLCS.Model.PathElements;
using DLCS.Web.Response;
using IIIF;
using IIIF.Auth.V2;
using IIIF.Presentation;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Annotation;
using IIIF.Presentation.V3.Content;
using IIIF.Presentation.V3.Strings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestrator.Settings;
using IIIFAuth2 = IIIF.Auth.V2;

namespace Orchestrator.Infrastructure.IIIF.Manifests;

/// <summary>
/// Implementation of <see cref="IBuildManifests{T}"/> responsible for generating IIIF v3 manifest
/// </summary>
public class ManifestV3Builder : IIIFManifestBuilderBase, IBuildManifests<Manifest>
{
    private readonly IIIIFAuthBuilder authBuilder;
    private readonly ILogger<ManifestV3Builder> logger;
    private const string Language = "en";

    public ManifestV3Builder(IAssetPathGenerator assetPathGenerator,
        IOptions<OrchestratorSettings> orchestratorSettings, IThumbSizeProvider thumbSizeProvider,
        IIIIFAuthBuilder authBuilder, ILogger<ManifestV3Builder> logger) : base(assetPathGenerator,
        orchestratorSettings, thumbSizeProvider)
    {
        this.authBuilder = authBuilder;
        this.logger = logger;
    }

    public async Task<Manifest> BuildManifest(string manifestId, string label, List<Asset> assets, CustomerPathElement customerPathElement,
        ManifestType manifestType, CancellationToken cancellationToken)
    {
        var probeServices = await GetProbeServices(assets, cancellationToken);
        var anyAssetRequireAuth = !probeServices.IsNullOrEmpty();
        
        var manifest = new Manifest
        {
            Id = manifestId,
            Label = new LanguageMap(Language, label),
            Metadata = GetManifestMetadata().ToV3Metadata(Language),
        };
        
        // TODO - add context dependant on whether BD style output is added
        
        manifest.EnsurePresentation3Context();
        if (anyAssetRequireAuth)
        {
            logger.LogTrace(
                "ManifestId {ManifestId} has at least 1 asset requiring auth - adding Auth2 context + services",
                manifestId);
            manifest.EnsureContext(IIIFAuth2.Constants.IIIFAuth2Context);

            // Add the AuthAccessServices to the manifest services collection.
            // Individual ImageServices will contain ProbeService and reference this accessService
            var accessServices = GetDistinctAccessServices(probeServices);
            manifest.Services = accessServices;
        }
        
        var canvases = await CreateCanvases(assets, customerPathElement, probeServices, cancellationToken);
        manifest.Items = canvases;
        manifest.Thumbnail = canvases.FirstOrDefault(c => !c.Thumbnail.IsNullOrEmpty())?.Thumbnail;
        
        return manifest;
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
        // Get a list of all _distinct_ access services - these are added at Manifest level. Canvases will reference
        var accessServices = probeServices!
            .SelectMany(kvp => kvp.Value.Service?.OfType<AuthAccessService2>() ?? Array.Empty<AuthAccessService2>())
            .DistinctBy(accessService => accessService.Id)
            .Cast<IService>()
            .ToList();
        return accessServices;
    }

    private async Task<List<Canvas>> CreateCanvases(List<Asset> results,
        CustomerPathElement customerPathElement, Dictionary<AssetId, AuthProbeService2>? authProbeServices,
        CancellationToken cancellationToken)
    {
        var probeServices = authProbeServices ?? new Dictionary<AssetId, AuthProbeService2>();
        int counter = 0;
        var canvases = new List<Canvas>(results.Count);
        foreach (var asset in results)
        {
            var canvas = await GetCanvas(asset, customerPathElement, ++counter, probeServices, cancellationToken);
            if (canvas != null) canvases.Add(canvas);
        }

        return canvases;
    }

    private async Task<Canvas?> GetCanvas(Asset asset, CustomerPathElement customerPathElement, int canvasIndex,
        Dictionary<AssetId, AuthProbeService2> authProbeServices, CancellationToken cancellationToken)
    {
        /*
         * If 'iiif-img'; add "Image" body on AnnotationPage>PaintingAnnotation
         * If 'thumbs'; add "Thumbnail" on canvas
         * If 'iiif-av' and single transcode; add "Sound" OR "Video" body on AnnotationPage>PaintingAnnotation
         * If 'iiif-av' and multi transcode; add "Choice" body on AnnotationPage>PaintingAnnotation
         * If 'file' _only_; add BD style placeholder body (incl behaviour at Canvas level)
         * If 'file'; add "Rendering" on canvas (regardless of what else is there)
         *
         * If 'iiif-av' and no AppMetadata then no Canvas - but _could_ have a BD style rendering if 'iiif-av' + 'file'
         *
         * Dimensions on Canvas can differ depending on type (temporal and/or spatial)
         * Metadata is same
         * CanvasId isn't same (do we want to change this??)
         *
         * Build something that returns: Thumbnail, Canvas, Rendering. Just whack those in as required.
         *    Does it need to return dimensions too?
         *
         * Auth services - on "body" too ALWAYS. On ImageService for images
         */

        // TODO - this should differ depending on type of asset
        var canvasId = GetCanvasId(asset, customerPathElement, canvasIndex);
        
        var canvasParts =
            await GetCanvasParts(asset, customerPathElement, canvasId, authProbeServices, cancellationToken);
        
        if (canvasParts.Thumbnail == null && canvasParts.AnnotationPage == null) return null;

        // The basic properties of a canvas are identical regardless of how the asset is available
        var canvas = new Canvas
        {
            Id = canvasId,
            Label = new LanguageMap("en", $"Canvas {canvasIndex}"),
            Width = asset.Width,
            Height = asset.Height,
            Duration = asset.Duration,
            Metadata = GetCanvasMetadata(asset).ToV3Metadata(MetadataLanguage),
            Items = canvasParts.AnnotationPage?.AsList(),
            Thumbnail = canvasParts.Thumbnail?.AsListOf<ExternalResource>(),
        };

        return canvas;
    }

    private async Task<CanvasParts> GetCanvasParts(Asset asset, CustomerPathElement customerPathElement, string canvasId,
        Dictionary<AssetId, AuthProbeService2> authProbeServices, CancellationToken cancellationToken)
    {
        var authServices = GetAuthServices(asset, authProbeServices);
        
        AnnotationPage? annotationPage = null;
        ExternalResource? thumbnail = null;
        
        // If Image or Thumbnail then it will have Image body and/or thumbnails
        if (asset.HasAnyDeliveryChannel(AssetDeliveryChannels.Image, AssetDeliveryChannels.Thumbnails))
        {
            var thumbnailSizes = await RetrieveThumbnails(asset, cancellationToken);

            annotationPage = new AnnotationPage
            {
                Id = $"{canvasId}/page",
                Items = new PaintingAnnotation
                {
                    Target = new Canvas { Id = canvasId },
                    Id = $"{canvasId}/page/image",
                    Body = new Image
                    {
                        Id = GetFullQualifiedImagePath(asset, customerPathElement,
                            thumbnailSizes.MaxDerivativeSize, false),
                        Format = "image/jpeg",
                        Width = thumbnailSizes.MaxDerivativeSize.Width,
                        Height = thumbnailSizes.MaxDerivativeSize.Height,
                        Service = asset.HasDeliveryChannel(AssetDeliveryChannels.Image) 
                            ? GetImageServices(asset, customerPathElement, false, authServices)
                            : null,
                    },
                }.AsListOf<IAnnotation>(),
            };

            if (ShouldAddThumbs(asset, thumbnailSizes))
            {
                thumbnail = new Image
                {
                    Id = GetFullQualifiedThumbPath(asset, customerPathElement, thumbnailSizes.OpenThumbnails),
                    Format = "image/jpeg",
                    Service = GetImageServiceForThumbnail(asset, customerPathElement, false,
                        thumbnailSizes.OpenThumbnails)
                };
            }
        }
        
        return new CanvasParts(annotationPage, thumbnail);
    }

    /// <summary>
    /// Carrier for the possible properties that can be added to a manifest, dependent on Asset config
    /// </summary>
    private record CanvasParts(AnnotationPage? AnnotationPage, ExternalResource? Thumbnail);
    
    private List<IService>? GetAuthServices(Asset asset, Dictionary<AssetId, AuthProbeService2> authProbeServices)
    {
        if (authProbeServices.IsNullOrEmpty()) return null;
        if (!authProbeServices.TryGetValue(asset.Id, out var probeService2)) return null;

        var authServiceToAdd = probeService2.ToEmbeddedService();
        return authServiceToAdd.AsListOf<IService>();
    }
}
