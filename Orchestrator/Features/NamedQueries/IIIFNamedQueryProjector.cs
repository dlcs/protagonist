using System.Collections.Generic;
using System.Linq;
using DLCS.Core.Collections;
using DLCS.Model.Assets;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Model.PathElements;
using DLCS.Web.Requests.AssetDelivery;
using DLCS.Web.Response;
using IIIF;
using IIIF.ImageApi.Service;
using IIIF.Presentation.V2.Annotation;
using IIIF.Presentation.V2.Strings;
using IIIF.Presentation.V3.Constants;
using Microsoft.Extensions.Options;
using Orchestrator.Infrastructure.NamedQueries;
using Orchestrator.Settings;
using IIIF2 = IIIF.Presentation.V2;

namespace Orchestrator.Features.NamedQueries
{
    /// <summary>
    /// Methods for generating IIIF results from 
    /// </summary>
    public class IIIFNamedQueryProjector
    {
        private readonly IAssetPathGenerator assetPathGenerator;
        private readonly OrchestratorSettings orchestratorSettings;

        public IIIFNamedQueryProjector(
            IAssetPathGenerator assetPathGenerator,
            IOptions<OrchestratorSettings> orchestratorSettings)
        {
            this.assetPathGenerator = assetPathGenerator;
            this.orchestratorSettings = orchestratorSettings.Value;
        }
        
        public JsonLdBase GenerateV2Manifest(NamedQueryResult result, string rootUrl)
        {
            var manifest = new IIIF2.Manifest
            {
                Label = new MetaDataValue("Title"),
                Metadata = new IIIF2.Metadata
                {
                    Label = new MetaDataValue("Title"), Value = new MetaDataValue("Created by DLCS") 
                }.AsList(),
                Sequences = new IIIF2.Sequence
                {
                    Id = string.Concat(rootUrl, "/iiif-query/sequence/0"),
                    Label = new MetaDataValue("Sequence 0"),
                    Canvases = CreateCanvases(result)
                }.AsList(),
            };

            manifest.EnsurePresentation2Context();
            return manifest;
        }

        private List<IIIF2.Canvas> CreateCanvases(NamedQueryResult result)
        {
            int counter = 0;
            var canvases = result.Results
                .OrderBy(i => GetCanvasOrderingElement(i, result.Query))
                .Select(i =>
                {
                    var fullyQualifiedImageId = GetFullyQualifiedId(i, result.Query.CustomerPathElement);
                    var imageExample = $"{fullyQualifiedImageId}/full/{i.Width},{i.Height}/0/default.jpg";
                    var canvasId = string.Concat(fullyQualifiedImageId, "/canvas/c/", ++counter);
                    return new IIIF2.Canvas
                    {
                        Id = canvasId,
                        Width = i.Width,
                        Height = i.Height,
                        Thumbnail = new IIIF2.Thumbnail
                        {
                            Id = GetFullQualifiedThumbPath(i, result.Query.CustomerPathElement)
                        }.AsList(),
                        Images = new ImageAnnotation
                        {
                            Id = string.Concat(fullyQualifiedImageId, "/imageanno/0"),
                            On = canvasId,
                            Resource = new IIIF2.ImageResource
                            {
                                Id = imageExample,
                                Width = i.Width,
                                Height = i.Height,
                                Service = new ImageService2
                                {
                                    Id = fullyQualifiedImageId,
                                    Profile = ImageService2.Level1Profile,
                                    Width = i.Width,
                                    Height = i.Height,
                                }.AsListOf<IService>()
                            }
                        }.AsList()
                    };
                }).ToList();

            return canvases;
        }

        private object GetCanvasOrderingElement(Asset image, ParsedNamedQuery query)
            => query.Canvas switch
            {
                ParsedNamedQuery.QueryMapping.Number1 => image.NumberReference1,
                ParsedNamedQuery.QueryMapping.Number2 => image.NumberReference2,
                ParsedNamedQuery.QueryMapping.Number3 => image.NumberReference3,
                ParsedNamedQuery.QueryMapping.String1 => image.Reference1,
                ParsedNamedQuery.QueryMapping.String2 => image.Reference2,
                ParsedNamedQuery.QueryMapping.String3 => image.Reference3,
                _ => 0
            };

        private string GetFullQualifiedThumbPath(Asset asset, CustomerPathElement customerPathElement)
        {
            var uniqueName = asset.GetUniqueName();
            var thumbRequest = new BasicPathElements
            {
                Space = asset.Space,
                AssetPath = $"{uniqueName}/full/full/0/default.jpg",
                RoutePrefix = orchestratorSettings.Proxy.ThumbsPath,
                CustomerPathValue = customerPathElement.Id.ToString(),
            };
            return assetPathGenerator.GetFullPathForRequest(thumbRequest);
        }

        private string GetFullyQualifiedId(Asset asset, CustomerPathElement customerPathElement)
        {
            var thumbRequest = new BasicPathElements
            {
                Space = asset.Space,
                AssetPath = asset.GetUniqueName(),
                RoutePrefix = orchestratorSettings.Proxy.ImagePath,
                CustomerPathValue = customerPathElement.Id.ToString(),
            };
            return assetPathGenerator.GetFullPathForRequest(thumbRequest);
        }
    }
}