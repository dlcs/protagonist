using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
using Version = IIIF.Presentation.Version;

namespace Orchestrator.Features.NamedQueries
{
    /// <summary>
    /// Methods for generating IIIF results from NamedQueries
    /// </summary>
    public class IIIFNamedQueryProjector
    {
        private readonly IAssetPathGenerator assetPathGenerator;
        private readonly IThumbnailPolicyRepository thumbnailPolicyRepository;
        private readonly OrchestratorSettings orchestratorSettings;
        private readonly Dictionary<string, ThumbnailPolicy> ThumbnailPolicies = new();

        public IIIFNamedQueryProjector(
            IAssetPathGenerator assetPathGenerator, 
            IOptions<OrchestratorSettings> orchestratorSettings,
            IThumbnailPolicyRepository thumbnailPolicyRepository)
        {
            this.assetPathGenerator = assetPathGenerator;
            this.thumbnailPolicyRepository = thumbnailPolicyRepository;
            this.orchestratorSettings = orchestratorSettings.Value;
        }

        /// <summary>
        /// Project NamedQueryResult to IIIF presentation object
        /// </summary>
        public async Task<JsonLdBase> GenerateIIIFPresentation(NamedQueryResult result, HttpRequest request,
            Version iiifPresentationVersion, string namedQueryName)
            => iiifPresentationVersion == Version.V2
                ? await GenerateV2Manifest(result, request, namedQueryName)
                : await GenerateV3Manifest(result, request, namedQueryName);
        
        
        private async Task<JsonLdBase> GenerateV2Manifest(NamedQueryResult result, HttpRequest request, string namedQueryName)
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
                    Canvases = await CreateV2Canvases(result)
                }.AsList(),
            };

            manifest.EnsurePresentation2Context();
            return manifest;
        }

        private async Task<JsonLdBase> GenerateV3Manifest(NamedQueryResult result, HttpRequest request, string namedQueryName)
        {
            const string language = "en";
            var manifest = new IIIF3.Manifest
            {
                Id = UriHelper.GetDisplayUrl(request),
                Label = new LanguageMap(language, $"Generated from '{namedQueryName}' named query"),
                Metadata = new LabelValuePair(language, "Title", "Created by DLCS").AsList(),
            };

            var canvases = await CreateV3Canvases(result);
            manifest.Items = canvases;
            manifest.Thumbnail = canvases.FirstOrDefault()?.Thumbnail;
            
            manifest.EnsurePresentation3Context();
            return manifest;
        }

        private async Task<List<IIIF3.Canvas>> CreateV3Canvases(NamedQueryResult result)
        {
            int counter = 0;
            var canvases = new List<IIIF3.Canvas>(result.Results.Count());
            foreach (var i in result.Results.OrderBy(i => GetCanvasOrderingElement(i, result.Query)))
            {
                var fullyQualifiedImageId = GetFullyQualifiedId(i, result.Query.CustomerPathElement);
                var canvasId = string.Concat(fullyQualifiedImageId, "/canvas/c/", ++counter);

                var canvas = new IIIF3.Canvas
                {
                    Id = canvasId,
                    Width = i.Width,
                    Height = i.Height,
                    Items = new AnnotationPage
                    {
                        Id = $"{canvasId}/anno/1",
                        Items = new PaintingAnnotation
                        {
                            Id = fullyQualifiedImageId,
                            Body = new Image
                            {
                                Id = GetFullQualifiedImagePath(i, result.Query.CustomerPathElement,
                                    new Size(i.Width, i.Height), false),
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

                var thumbnailSizes = await GetThumbnailSizesForImage(i);
                if (!thumbnailSizes.IsNullOrEmpty())
                {
                    canvas.Thumbnail = new IIIF3.Content.Image
                    {
                        Id = GetFullQualifiedThumbServicePath(i, result.Query.CustomerPathElement),
                        Format = "image/jpeg",
                        Service = new ImageService2
                        {
                            Id = GetFullQualifiedThumbPath(i, result.Query.CustomerPathElement, thumbnailSizes),
                            Profile = ImageService2.Level0Profile,
                            Sizes = thumbnailSizes
                        }.AsListOf<IService>()
                    }.AsListOf<ExternalResource>();
                }
                
                canvases.Add(canvas);
            }

            return canvases;
        }

        private async Task<List<IIIF2.Canvas>> CreateV2Canvases(NamedQueryResult result)
        {
            int counter = 0;
            var canvases = new List<IIIF2.Canvas>(result.Results.Count());
            foreach (var i in result.Results.OrderBy(i => GetCanvasOrderingElement(i, result.Query)))
            {
                var fullyQualifiedImageId = GetFullyQualifiedId(i, result.Query.CustomerPathElement);
                var canvasId = string.Concat(fullyQualifiedImageId, "/canvas/c/", ++counter);
                var canvas = new IIIF2.Canvas
                {
                    Id = canvasId,
                    Width = i.Width,
                    Height = i.Height,
                    Images = new ImageAnnotation
                    {
                        Id = string.Concat(fullyQualifiedImageId, "/imageanno/0"),
                        On = canvasId,
                        Resource = new IIIF2.ImageResource
                        {
                            Id = GetFullQualifiedImagePath(i, result.Query.CustomerPathElement,
                                new Size(i.Width, i.Height), false),
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

                var thumbnailSizes = await GetThumbnailSizesForImage(i);
                if (!thumbnailSizes.IsNullOrEmpty())
                {
                    canvas.Thumbnail = new IIIF2.Thumbnail
                    {
                        Id = GetFullQualifiedThumbServicePath(i, result.Query.CustomerPathElement)
                    }.AsList();
                }
                
                canvases.Add(canvas);
            }

            return canvases;
        }

        private async Task<List<Size>> GetThumbnailSizesForImage(Asset image)
        {
            var thumbnailPolicy = await GetThumbnailPolicyForImage(image);
            return image.GetAvailableThumbSizes(thumbnailPolicy, out _);
        }
        
        private async Task<ThumbnailPolicy> GetThumbnailPolicyForImage(Asset image)
        {
            if (ThumbnailPolicies.TryGetValue(image.ThumbnailPolicy, out var thumbnailPolicy))
            {
                return thumbnailPolicy;
            }

            var thumbnailPolicyFromDb = await thumbnailPolicyRepository.GetThumbnailPolicy(image.ThumbnailPolicy);
            ThumbnailPolicies[image.ThumbnailPolicy] = thumbnailPolicyFromDb;
            return thumbnailPolicyFromDb;
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

        private string GetFullQualifiedThumbServicePath(Asset asset, CustomerPathElement customerPathElement)
        {
            var thumbRequest = new BasicPathElements
            {
                Space = asset.Space,
                AssetPath = asset.GetUniqueName(),
                RoutePrefix = orchestratorSettings.Proxy.ThumbsPath,
                CustomerPathValue = customerPathElement.Id.ToString(),
            };
            return assetPathGenerator.GetFullPathForRequest(thumbRequest);
        }
        
        private string GetFullQualifiedThumbPath(Asset asset, CustomerPathElement customerPathElement, 
            List<Size> availableThumbs)
        {
            var targetThumb = orchestratorSettings.TargetThumbnailSize;
            
            // Get the thumbnail size that is closest to the system-wide TargetThumbnailSize
            var closestSize = availableThumbs
                .OrderBy(s => s.MaxDimension)
                .Aggregate((x, y) =>
                    Math.Abs(x.MaxDimension - targetThumb) < Math.Abs(y.MaxDimension - targetThumb) ? x : y);

            return GetFullQualifiedImagePath(asset, customerPathElement, closestSize, true);
        }

        private string GetFullQualifiedImagePath(Asset asset, CustomerPathElement customerPathElement, Size size,
            bool isThumb)
        {
            var request = new BasicPathElements
            {
                Space = asset.Space,
                AssetPath = $"{asset.GetUniqueName()}/full/{size.Width},{size.Height}/0/default.jpg",
                RoutePrefix = isThumb ? orchestratorSettings.Proxy.ThumbsPath : orchestratorSettings.Proxy.ImagePath,
                CustomerPathValue = customerPathElement.Id.ToString(),
            };
            return assetPathGenerator.GetFullPathForRequest(request);
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