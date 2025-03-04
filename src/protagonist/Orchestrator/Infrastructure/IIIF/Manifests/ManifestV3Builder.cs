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
    
    private async Task<Dictionary<AssetId, AuthProbeService2>?> GetProbeServices(IReadOnlyCollection<Asset> assets, CancellationToken cancellationToken)
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
        int counter = 0;
        var canvases = new List<Canvas>(results.Count);
        foreach (var asset in results)
        {
            var canvasId = GetCanvasId(asset, customerPathElement, ++counter);
            var thumbnailSizes = await RetrieveThumbnails(asset, cancellationToken);

            var canvas = new Canvas
            {
                Id = canvasId,
                Label = new LanguageMap("en", $"Canvas {counter}"),
                Width = asset.Width,
                Height = asset.Height,
                Metadata = GetCanvasMetadata(asset).ToV3Metadata(MetadataLanguage),
                Items = new AnnotationPage
                {
                    Id = $"{canvasId}/page",
                    Items = new PaintingAnnotation
                    {
                        Target = new Canvas { Id = canvasId },
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
                canvas.Thumbnail = new Image
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
}
