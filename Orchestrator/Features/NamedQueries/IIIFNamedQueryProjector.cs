using System.Collections.Generic;
using System.Linq;
using DLCS.Core.Collections;
using DLCS.Model.Assets;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Model.PathElements;
using DLCS.Web.Requests;
using DLCS.Web.Requests.AssetDelivery;
using DLCS.Web.Response;
using IIIF;
using IIIF.ImageApi.Service;
using IIIF.Presentation;
using IIIF.Presentation.V2.Annotation;
using IIIF.Presentation.V2.Strings;
using IIIF.Presentation.V3.Annotation;
using IIIF.Presentation.V3.Content;
using IIIF.Presentation.V3.Strings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Options;
using Orchestrator.Infrastructure.NamedQueries;
using Orchestrator.Settings;
using IIIF2 = IIIF.Presentation.V2;
using IIIF3 = IIIF.Presentation.V3;

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

        /// <summary>
        /// Project NamedQueryResult to IIIF presentation object
        /// </summary>
        public JsonLdBase GenerateIIIFPresentation(NamedQueryResult result, HttpRequest request,
            Version iiifPresentationVersion, string namedQueryName)
            => iiifPresentationVersion == Version.V2
                ? GenerateV2Manifest(result, request, namedQueryName)
                : GenerateV3Manifest(result, request, namedQueryName);
        
        
        public JsonLdBase GenerateV2Manifest(NamedQueryResult result, HttpRequest request, string namedQueryName)
        {
            var rootUrl = HttpRequestX.GetDisplayUrl(request);
            var manifest = new IIIF2.Manifest
            {
                Id = UriHelper.GetDisplayUrl(request),
                Label = new MetaDataValue($"Generated from '{namedQueryName}' named query"),
                Metadata = new IIIF2.Metadata
                {
                    Label = new MetaDataValue("Title"), Value = new MetaDataValue("Created by DLCS") 
                }.AsList(),
                Sequences = new IIIF2.Sequence
                {
                    Id = string.Concat(rootUrl, "/iiif-query/sequence/0"),
                    Label = new MetaDataValue("Sequence 0"),
                    Canvases = CreateV2Canvases(result)
                }.AsList(),
            };

            manifest.EnsurePresentation2Context();
            return manifest;
        }

        public JsonLdBase GenerateV3Manifest(NamedQueryResult result, HttpRequest request, string namedQueryName)
        {
            const string language = "en";
            var manifest = new IIIF3.Manifest
            {
                Id = UriHelper.GetDisplayUrl(request),
                Label = new LanguageMap(language, $"Generated from '{namedQueryName}' named query"),
                Metadata = new LabelValuePair(language, "Title", "Created by DLCS").AsList(),
                Items = CreateV3Canvases(result)
            };
            
            manifest.EnsurePresentation3Context();
            return manifest;
        }

        private List<IIIF3.Canvas> CreateV3Canvases(NamedQueryResult result)
        {
            int counter = 0;
            var canvases = result.Results
                .OrderBy(i => GetCanvasOrderingElement(i, result.Query))
                .Select(i =>
                {
                    var fullyQualifiedImageId = GetFullyQualifiedId(i, result.Query.CustomerPathElement);
                    var imageExample = $"{fullyQualifiedImageId}/full/{i.Width},{i.Height}/0/default.jpg";
                    var canvasId = string.Concat(fullyQualifiedImageId, "/canvas/c/", ++counter);

                    return new IIIF3.Canvas
                    {
                        Id = canvasId,
                        Width = i.Width,
                        Height = i.Height,
                        Thumbnail = new IIIF3.Content.Image
                        {
                            Id = GetFullQualifiedThumbPath(i, result.Query.CustomerPathElement),
                            Format = "image/jpeg",
                            Service = new ImageService2
                            {
                                Id = GetFullQualifiedThumbPath(i, result.Query.CustomerPathElement, true),
                                Profile = ImageService2.Level1Profile
                            }.AsListOf<IService>()
                        }.AsListOf<ExternalResource>(),
                        Items = new AnnotationPage
                        {
                            Id = $"{canvasId}/anno/1",
                            Items = new PaintingAnnotation
                            {
                                Id = fullyQualifiedImageId,
                                Body = new Image
                                {
                                    Id = imageExample,
                                    Format = "image/jpeg",
                                    Service = new ImageService2
                                    {
                                        Id = fullyQualifiedImageId,
                                        Profile = ImageService2.Level1Profile,
                                        Width = i.Width,
                                        Height = i.Height,
                                    }.AsListOf<IService>()
                                }
                            }.AsListOf<IAnnotation>()
                        }.AsList()
                    };
                }).ToList();
            return canvases;
        }

        private List<IIIF2.Canvas> CreateV2Canvases(NamedQueryResult result)
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

        private string GetFullQualifiedThumbPath(Asset asset, CustomerPathElement customerPathElement,
            bool serviceOnly = false)
        {
            var uniqueName = asset.GetUniqueName();
            var thumbRequest = new BasicPathElements
            {
                Space = asset.Space,
                AssetPath = serviceOnly ? uniqueName : $"{uniqueName}/full/full/0/default.jpg",
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