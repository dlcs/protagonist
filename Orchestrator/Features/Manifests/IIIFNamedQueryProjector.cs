using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Collections;
using DLCS.Core.Guard;
using DLCS.Model.Assets;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Web.Requests;
using IIIF;
using IIIF.Presentation;
using IIIF.Presentation.V2.Strings;
using IIIF.Presentation.V3.Strings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.EntityFrameworkCore;
using Orchestrator.Features.Manifests.Requests;
using Orchestrator.Infrastructure.IIIF;
using Orchestrator.Infrastructure.NamedQueries;
using IIIF2 = IIIF.Presentation.V2;
using IIIF3 = IIIF.Presentation.V3;
using Version = IIIF.Presentation.Version;

namespace Orchestrator.Features.Manifests
{
    /// <summary>
    /// Methods for generating IIIF results from NamedQueries
    /// </summary>
    public class IIIFNamedQueryProjector
    {
        private readonly IIIFCanvasFactory canvasFactory;

        public IIIFNamedQueryProjector(IIIFCanvasFactory canvasFactory) 
        {
            this.canvasFactory = canvasFactory;
        }

        /// <summary>
        /// Project NamedQueryResult to IIIF presentation object
        /// </summary>
        public async Task<JsonLdBase?> GenerateIIIFPresentation(NamedQueryResult<IIIFParsedNamedQuery> namedQueryResult,
            HttpRequest request, Version iiifPresentationVersion, CancellationToken cancellationToken = default)
        {
            var parsedNamedQuery = namedQueryResult.ParsedQuery.ThrowIfNull(nameof(request.Query))!;

            var assets = await namedQueryResult.Results.ToListAsync(cancellationToken);
            if (assets.Count == 0) return null;

            var orderedImages = NamedQueryProjections.GetOrderedAssets(assets, parsedNamedQuery).ToList();

            return iiifPresentationVersion == Version.V2
                ? await GenerateV2Manifest(parsedNamedQuery, orderedImages, request)
                : await GenerateV3Manifest(parsedNamedQuery, orderedImages, request);
        }

        private async Task<JsonLdBase> GenerateV2Manifest(IIIFParsedNamedQuery parsedNamedQuery, List<Asset> results,
            HttpRequest request)
        {
            var rootUrl = HttpRequestX.GetDisplayUrl(request);
            var manifest = new IIIF2.Manifest
            {
                Id = UriHelper.GetDisplayUrl(request),
                Label = new MetaDataValue($"Generated from '{parsedNamedQuery.NamedQueryName}' named query"),
                Metadata = new IIIF2.Metadata
                {
                    Label = new MetaDataValue("Title"), Value = new MetaDataValue("Created by DLCS")
                }.AsList(),
            };

            var canvases = await canvasFactory.CreateV2Canvases(results, parsedNamedQuery.CustomerPathElement);
            var sequence = new IIIF2.Sequence
            {
                Id = string.Concat(rootUrl, "/iiif-query/sequence/0"),
                Label = new MetaDataValue("Sequence 0"),
            };
            sequence.Canvases = canvases;
            manifest.Thumbnail = canvases.FirstOrDefault(c => !c.Thumbnail.IsNullOrEmpty())?.Thumbnail;
            manifest.Sequences = sequence.AsList();

            manifest.EnsurePresentation2Context();
            return manifest;
        }

        private async Task<JsonLdBase> GenerateV3Manifest(IIIFParsedNamedQuery parsedNamedQuery,
            List<Asset> results, HttpRequest request)
        {
            const string language = "en";
            var manifest = new IIIF3.Manifest
            {
                Id = UriHelper.GetDisplayUrl(request),
                Label = new LanguageMap(language, $"Generated from '{parsedNamedQuery.NamedQueryName}' named query"),
                Metadata = new LabelValuePair(language, "Title", "Created by DLCS").AsList(),
            };

            var canvases = await canvasFactory.CreateV3Canvases(results, parsedNamedQuery.CustomerPathElement);
            manifest.Items = canvases;
            manifest.Thumbnail = canvases.FirstOrDefault(c => !c.Thumbnail.IsNullOrEmpty())?.Thumbnail;
            
            manifest.EnsurePresentation3Context();
            return manifest;
        }
    }
}