using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Collections;
using DLCS.Core.Guard;
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
using Microsoft.EntityFrameworkCore;
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
        private readonly Dictionary<string, ThumbnailPolicy> thumbnailPolicies = new();

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
        public async Task<JsonLdBase?> GenerateIIIFPresentation(NamedQueryResult<IIIFParsedNamedQuery> result,
            HttpRequest request, Version iiifPresentationVersion, string namedQueryName,
            CancellationToken cancellationToken = default)
        {
            result.Query.ThrowIfNull(nameof(request.Query));
            
            var imageResults = await result.Results.ToListAsync(cancellationToken);

            if (imageResults.Count == 0) return null;

            return iiifPresentationVersion == Version.V2
                ? await GenerateV2Manifest(result.Query!, imageResults, request, namedQueryName)
                : await GenerateV3Manifest(result.Query!, imageResults, request, namedQueryName);
        }

        private async Task<JsonLdBase> GenerateV2Manifest(IIIFParsedNamedQuery parsedNamedQuery,
            List<Asset> results, HttpRequest request, string namedQueryName)
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
            };

            var canvases = await CreateV2Canvases(parsedNamedQuery, results);
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
            List<Asset> results, HttpRequest request, string namedQueryName)
        {
            const string language = "en";
            var manifest = new IIIF3.Manifest
            {
                Id = UriHelper.GetDisplayUrl(request),
                Label = new LanguageMap(language, $"Generated from '{namedQueryName}' named query"),
                Metadata = new LabelValuePair(language, "Title", "Created by DLCS").AsList(),
            };

            var canvases = await CreateV3Canvases(parsedNamedQuery, results);
            manifest.Items = canvases;
            manifest.Thumbnail = canvases.FirstOrDefault(c => !c.Thumbnail.IsNullOrEmpty())?.Thumbnail;
            
            manifest.EnsurePresentation3Context();
            return manifest;
        }

        private async Task<List<IIIF3.Canvas>> CreateV3Canvases(IIIFParsedNamedQuery parsedNamedQuery,
            List<Asset> results)
        {
            int counter = 0;
            var canvases = new List<IIIF3.Canvas>(results.Count);
            foreach (var i in results.OrderBy(i => GetCanvasOrderingElement(i, parsedNamedQuery)))
            {
                var fullyQualifiedImageId = GetFullyQualifiedId(i, parsedNamedQuery.CustomerPathElement);
                var canvasId = string.Concat(fullyQualifiedImageId, "/canvas/c/", ++counter);
                var thumbnailSizes = await GetThumbnailSizesForImage(i);

                var canvas = new IIIF3.Canvas
                {
                    Id = canvasId,
                    Width = i.Width,
                    Height = i.Height,
                    Items = new AnnotationPage
                    {
                        Id = $"{canvasId}/page",
                        Items = new PaintingAnnotation
                        {
                            Id = $"{canvasId}/page/image",
                            Body = new Image
                            {
                                Id = GetFullQualifiedImagePath(i, parsedNamedQuery.CustomerPathElement,
                                    thumbnailSizes.MaxDerivativeSize, false),
                                Format = "image/jpeg",
                                Service = new ImageService2
                                {
                                    Id = fullyQualifiedImageId,
                                    Profile = ImageService2.Level1Profile,
                                    Context = ImageService2.Image2Context,
                                    Width = i.Width,
                                    Height = i.Height,
                                }.AsListOf<IService>()
                            }
                        }.AsListOf<IAnnotation>()
                    }.AsList()
                };

                if (!thumbnailSizes.OpenThumbnails.IsNullOrEmpty())
                {
                    canvas.Thumbnail = new IIIF3.Content.Image
                    {
                        Id = GetFullQualifiedThumbServicePath(i, parsedNamedQuery.CustomerPathElement),
                        Format = "image/jpeg",
                        Service = GetImageServiceForThumbnail(i, parsedNamedQuery.CustomerPathElement,
                            thumbnailSizes.OpenThumbnails)
                    }.AsListOf<ExternalResource>();
                }
                
                canvases.Add(canvas);
            }

            return canvases;
        }

        private async Task<List<IIIF2.Canvas>> CreateV2Canvases(IIIFParsedNamedQuery parsedNamedQuery,
            List<Asset> results)
        {
            int counter = 0;
            var canvases = new List<IIIF2.Canvas>(results.Count);
            foreach (var i in results.OrderBy(i => GetCanvasOrderingElement(i, parsedNamedQuery)))
            {
                var fullyQualifiedImageId = GetFullyQualifiedId(i, parsedNamedQuery.CustomerPathElement);
                var canvasId = string.Concat(fullyQualifiedImageId, "/canvas/c/", ++counter);
                var thumbnailSizes = await GetThumbnailSizesForImage(i);

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
                            Id = GetFullQualifiedImagePath(i, parsedNamedQuery.CustomerPathElement,
                                thumbnailSizes.MaxDerivativeSize, false),
                            Width = i.Width,
                            Height = i.Height,
                            Service = new ImageService2
                            {
                                Id = fullyQualifiedImageId,
                                Profile = ImageService2.Level1Profile,
                                Context = ImageService2.Image2Context,
                                Width = i.Width,
                                Height = i.Height,
                            }.AsListOf<IService>()
                        }
                    }.AsList()
                };

                if (!thumbnailSizes.OpenThumbnails.IsNullOrEmpty())
                {
                    canvas.Thumbnail = new IIIF2.Thumbnail
                    {
                        Id = GetFullQualifiedThumbServicePath(i, parsedNamedQuery.CustomerPathElement),
                        Service = GetImageServiceForThumbnail(i, parsedNamedQuery.CustomerPathElement,
                            thumbnailSizes.OpenThumbnails)
                    }.AsList();
                }

                canvases.Add(canvas);
            }

            return canvases;
        }

        private List<IService> GetImageServiceForThumbnail(Asset asset, CustomerPathElement customerPathElement,
            List<Size> thumbnailSizes) =>
            new ImageService2
            {
                Id = GetFullQualifiedThumbPath(asset, customerPathElement, thumbnailSizes),
                Profile = ImageService2.Level0Profile,
                Sizes = thumbnailSizes,
                Context = ImageService2.Image2Context,
            }.AsListOf<IService>();

        private async Task<ImageSizeDetails> GetThumbnailSizesForImage(Asset image)
        {
            var thumbnailPolicy = await GetThumbnailPolicyForImage(image);
            var thumbnailSizesForImage = image.GetAvailableThumbSizes(thumbnailPolicy, out var maxDimensions);

            if (thumbnailSizesForImage.IsNullOrEmpty())
            {
                var largestThumbnail = thumbnailPolicy.SizeList.OrderByDescending(s => s).First();

                return new ImageSizeDetails
                {
                    OpenThumbnails = new List<Size>(0),
                    IsDerivativeOpen = false,
                    MaxDerivativeSize = Size.Confine(largestThumbnail, new Size(image.Width, image.Height))
                };
            }

            return new ImageSizeDetails
            {
                OpenThumbnails = thumbnailSizesForImage,
                IsDerivativeOpen = true,
                MaxDerivativeSize = new Size(maxDimensions.maxAvailableWidth, maxDimensions.maxAvailableHeight)
            };
        }

        private async Task<ThumbnailPolicy> GetThumbnailPolicyForImage(Asset image)
        {
            if (thumbnailPolicies.TryGetValue(image.ThumbnailPolicy, out var thumbnailPolicy))
            {
                return thumbnailPolicy;
            }

            var thumbnailPolicyFromDb = await thumbnailPolicyRepository.GetThumbnailPolicy(image.ThumbnailPolicy);
            thumbnailPolicies[image.ThumbnailPolicy] = thumbnailPolicyFromDb;
            return thumbnailPolicyFromDb;
        }

        private object GetCanvasOrderingElement(Asset image, IIIFParsedNamedQuery query)
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
        
        /// <summary>
        /// Class containing details of available thumbnail sizes
        /// </summary>
        private class ImageSizeDetails
        {
            /// <summary>
            /// List of open availabel thumbnails
            /// </summary>
            public List<Size> OpenThumbnails { get; set; }
            
            /// <summary>
            /// The size of the largest derivative, according to thumbnail policy.
            /// </summary>
            public Size MaxDerivativeSize { get; set; }

            /// <summary>
            /// Whether the <see cref="MaxDerivativeSize"/> is open.
            /// </summary>
            public bool IsDerivativeOpen { get; set; }
        }
    }
}