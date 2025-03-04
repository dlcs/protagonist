using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Guard;
using DLCS.Model.Assets;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Model.PathElements;
using DLCS.Web.Requests;
using IIIF;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.EntityFrameworkCore;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.IIIF;
using Orchestrator.Infrastructure.IIIF.Manifests;
using Orchestrator.Infrastructure.NamedQueries;
using Version = IIIF.Presentation.Version;

namespace Orchestrator.Features.Manifests;

/// <summary>
/// Methods for generating IIIF results from NamedQueries
/// </summary>
public class IIIFNamedQueryProjector
{
    private readonly IIIFManifestBuilder manifestBuilder;

    public IIIFNamedQueryProjector(IIIFManifestBuilder manifestBuilder) 
    {
        this.manifestBuilder = manifestBuilder;
    }

    /// <summary>
    /// Project NamedQueryResult to IIIF presentation object
    /// </summary>
    public async Task<JsonLdBase?> GenerateIIIFPresentation(NamedQueryResult<IIIFParsedNamedQuery> namedQueryResult,
        CustomerPathElement customerPathElement, HttpRequest request, Version iiifPresentationVersion, 
        CancellationToken cancellationToken = default)
    {
        var parsedNamedQuery = namedQueryResult.ParsedQuery.ThrowIfNull(nameof(request.Query))!;

        var assets = await namedQueryResult.Results
            .IncludeRelevantMetadataData()
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
        if (assets.Count == 0) return null;

        var orderedImages = NamedQueryProjections.GetOrderedAssets(assets, parsedNamedQuery).ToList();

        return iiifPresentationVersion == Version.V2
            ? await GenerateV2Manifest(parsedNamedQuery, customerPathElement, orderedImages, request, cancellationToken)
            : await GenerateV3Manifest(parsedNamedQuery, customerPathElement, orderedImages, request, cancellationToken);
    }

    private async Task<JsonLdBase> GenerateV2Manifest(IIIFParsedNamedQuery parsedNamedQuery,
        CustomerPathElement customerPathElement, List<Asset> results, HttpRequest request,
        CancellationToken cancellationToken)
    {
        var manifestId = GetManifestId(request);
        var label = GetManifestLabel(parsedNamedQuery);
        var manifest = await manifestBuilder.GenerateV2Manifest(results, customerPathElement, manifestId, label,
            ManifestType.NamedQuery, cancellationToken);
        
        return manifest;
    }

    private async Task<JsonLdBase> GenerateV3Manifest(IIIFParsedNamedQuery parsedNamedQuery,
        CustomerPathElement customerPathElement, List<Asset> results, HttpRequest request,
        CancellationToken cancellationToken)
    {
        var manifestId = GetManifestId(request);
        var label = GetManifestLabel(parsedNamedQuery);
        var manifest =
            await manifestBuilder.GenerateV3Manifest(results, customerPathElement, manifestId, label,
                ManifestType.NamedQuery, cancellationToken);
        
        return manifest;
    }

    private static string GetManifestId(HttpRequest request) =>
        request.GetDisplayUrl(request.Path.Value, includeQueryParams: false);

    private static string GetManifestLabel(IIIFParsedNamedQuery parsedNamedQuery)
        => $"Generated from '{parsedNamedQuery.NamedQueryName}' named query";
}
