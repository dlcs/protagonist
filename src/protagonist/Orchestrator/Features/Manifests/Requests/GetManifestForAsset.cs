﻿using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Collections;
using DLCS.Model.Assets;
using DLCS.Repository;
using DLCS.Web.Requests.AssetDelivery;
using DLCS.Web.Response;
using IIIF;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.IIIF;
using Orchestrator.Infrastructure.Mediatr;
using Orchestrator.Models;
using IIIF2 = IIIF.Presentation.V2;
using IIIF3 = IIIF.Presentation.V3;
using Version = IIIF.Presentation.Version;

namespace Orchestrator.Features.Manifests.Requests;

/// <summary>
/// Mediatr request for generating basic single-item manifest for specified image
/// </summary>
public class GetManifestForAsset : IRequest<DescriptionResourceResponse>, IGenericAssetRequest
{
    public string FullPath { get; }

    public BaseAssetRequest AssetRequest { get; set; }

    public Version IIIFPresentationVersion { get; }

    public GetManifestForAsset(string path, Version iiifVersion)
    {
        FullPath = path;
        IIIFPresentationVersion = iiifVersion;
    }
}

public class GetManifestForAssetHandler : IRequestHandler<GetManifestForAsset, DescriptionResourceResponse>
{
    private readonly DlcsContext dlcsContext;
    private readonly IAssetPathGenerator assetPathGenerator;
    private readonly IIIFManifestBuilder manifestBuilder;
    private readonly ILogger<GetManifestForAssetHandler> logger;
    private const string ManifestLabel = "Generated by DLCS";

    public GetManifestForAssetHandler(
        DlcsContext dlcsContext,
        IAssetPathGenerator assetPathGenerator,
        IIIFManifestBuilder manifestBuilder,
        ILogger<GetManifestForAssetHandler> logger)
    {
        this.dlcsContext = dlcsContext;
        this.assetPathGenerator = assetPathGenerator;
        this.manifestBuilder = manifestBuilder;
        this.logger = logger;
    }

    public async Task<DescriptionResourceResponse> Handle(GetManifestForAsset request,
        CancellationToken cancellationToken)
    {
        var assetId = request.AssetRequest.GetAssetId();

        var asset = await dlcsContext.Images
            .IncludeDataForThumbs()
            .FirstOrDefaultAsync(a => a.Id == assetId, cancellationToken);
        
        if (asset == null || asset.NotForDelivery ||
            !asset.HasAnyDeliveryChannel(AssetDeliveryChannels.Image, AssetDeliveryChannels.Thumbnails))
        {
            logger.LogDebug("Attempted to request an iiif-manifest for {AssetId}, but it was not found or is unavailable on any image delivery channel.",
                assetId);
            return DescriptionResourceResponse.Empty;
        }    

        JsonLdBase manifest = request.IIIFPresentationVersion == Version.V3
            ? await GenerateV3Manifest(request.AssetRequest, asset, cancellationToken)
            : await GenerateV2Manifest(request.AssetRequest, asset, cancellationToken);

        return DescriptionResourceResponse.Open(manifest);
    }

    private async Task<IIIF3.Manifest> GenerateV3Manifest(BaseAssetRequest assetRequest, Asset asset,
        CancellationToken cancellationToken)
    {
        var manifestId = GetFullyQualifiedId(assetRequest);
        var manifest =
            await manifestBuilder.GenerateV3Manifest(asset.AsList(), assetRequest.Customer, manifestId, ManifestLabel,
                cancellationToken);

        return manifest;
    }

    private async Task<IIIF2.Manifest> GenerateV2Manifest(BaseAssetRequest assetRequest, Asset asset,
        CancellationToken cancellationToken)
    {
        var manifestIdAndSequenceRoot = GetFullyQualifiedId(assetRequest);
        var manifest =
            await manifestBuilder.GenerateV2Manifest(asset.AsList(), assetRequest.Customer, manifestIdAndSequenceRoot,
                ManifestLabel, manifestIdAndSequenceRoot, cancellationToken);
        return manifest;
    }

    private string GetFullyQualifiedId(BaseAssetRequest baseAssetRequest)
        => assetPathGenerator.GetFullPathForRequest(baseAssetRequest, true, false);
}