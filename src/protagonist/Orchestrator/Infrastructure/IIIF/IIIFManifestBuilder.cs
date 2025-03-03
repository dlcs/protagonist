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
using IIIF;
using IIIF.Presentation;
using IIIF.Presentation.V2.Strings;
using IIIF.Presentation.V3.Strings;
using Microsoft.Extensions.Logging;
using IIIF2 = IIIF.Presentation.V2;
using IIIF3 = IIIF.Presentation.V3;
using IIIFAuth2 = IIIF.Auth.V2;

namespace Orchestrator.Infrastructure.IIIF;

/// <summary>
/// Class for creating IIIF Manifests from provided assets 
/// </summary>
public class IIIFManifestBuilder
{
    private readonly IIIFCanvasFactory canvasFactory;
    private readonly IIIIFAuthBuilder authBuilder;
    private readonly ILogger<IIIFManifestBuilder> logger;
    private const string Language = "en";

    public IIIFManifestBuilder(IIIFCanvasFactory canvasFactory, IIIIFAuthBuilder authBuilder,
        ILogger<IIIFManifestBuilder> logger)
    {
        this.canvasFactory = canvasFactory;
        this.authBuilder = authBuilder;
        this.logger = logger;
    }

    public async Task<IIIF3.Manifest> GenerateV3Manifest(List<Asset> assets, CustomerPathElement customerPathElement,
        string manifestId, string label, CancellationToken cancellationToken)
    {
        var probeServices = await GetProbeServices(assets, cancellationToken);
        var anyAssetRequireAuth = !probeServices.IsNullOrEmpty();
        
        var manifest = new IIIF3.Manifest
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
        
        var canvases = await canvasFactory.CreateV3Canvases(assets, customerPathElement, probeServices);
        manifest.Items = canvases;
        manifest.Thumbnail = canvases.FirstOrDefault(c => !c.Thumbnail.IsNullOrEmpty())?.Thumbnail;
        
        return manifest;
    }

    public async Task<IIIF2.Manifest> GenerateV2Manifest(List<Asset> assets, CustomerPathElement customerPathElement,
        string manifestId, string label, string sequenceRoot, CancellationToken cancellationToken)
    {
        var manifest = new IIIF2.Manifest
        {
            Id = manifestId,
            Label = new MetaDataValue(label),
            Metadata = GetManifestMetadata().ToV2Metadata(),
        };
        
        manifest.EnsurePresentation2Context();
        
        var canvases = await canvasFactory.CreateV2Canvases(assets, customerPathElement);
        var sequence = new IIIF2.Sequence
        {
            Id = string.Concat(sequenceRoot, "/sequence/0"),
            Label = new MetaDataValue("Sequence 0"),
        };
        sequence.Canvases = canvases;
        manifest.Thumbnail = canvases.FirstOrDefault(c => !c.Thumbnail.IsNullOrEmpty())?.Thumbnail;
        manifest.Sequences = sequence.AsList();

        return manifest;
    }

    private static Dictionary<string, string> GetManifestMetadata() =>
        new()
        {
            ["Title"] = "Created by DLCS",
            ["Generated On"] = DateTime.UtcNow.ToString("u"),
        };

    private async Task<Dictionary<AssetId, IIIFAuth2.AuthProbeService2>?> GetProbeServices(IReadOnlyCollection<Asset> assets, CancellationToken cancellationToken)
    {
        var assetsRequiringAuth = assets.Where(a => a.RequiresAuth && !string.IsNullOrEmpty(a.Roles)).ToList();

        var assetsRequiringAuthCount = assetsRequiringAuth.Count;
        if (assetsRequiringAuthCount == 0) return null;

        var logLevel = assetsRequiringAuthCount > 10 ? LogLevel.Information : LogLevel.Debug;
        logger.Log(logLevel, "Getting Auth services for {AuthAssetCount} assets", assetsRequiringAuthCount);

        // This is doing a lot - batch the requests up?
        var sw = Stopwatch.StartNew();
        var probeServices = new Dictionary<AssetId, IIIFAuth2.AuthProbeService2>(assetsRequiringAuthCount);
        var taskList = new List<Task>(assetsRequiringAuthCount);
        foreach (var asset in assetsRequiringAuth)
        {
            taskList.Add(authBuilder.GetAuthServicesForAsset(asset.Id, asset.RolesList.ToList(), cancellationToken)
                .ContinueWith(antecedent =>
                    {
                        if (antecedent.Result is IIIFAuth2.AuthProbeService2 probeService2)
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
    
    private static List<IService> GetDistinctAccessServices(Dictionary<AssetId, IIIFAuth2.AuthProbeService2>? probeServices)
    {
        var accessServices = probeServices!
            .SelectMany(kvp => kvp.Value.Service?.OfType<IIIFAuth2.AuthAccessService2>() ?? Array.Empty<IIIFAuth2.AuthAccessService2>())
            .DistinctBy(accessService => accessService.Id)
            .Cast<IService>()
            .ToList();
        return accessServices;
    }
}
