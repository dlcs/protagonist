﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DLCS.Core.Collections;
using DLCS.Model.Assets;
using DLCS.Model.PathElements;
using DLCS.Web.Requests.AssetDelivery;
using DLCS.Web.Response;
using IIIF;
using IIIF.ImageApi.Service;
using IIIF.Presentation.V2.Annotation;
using IIIF.Presentation.V2.Strings;
using IIIF.Presentation.V3.Annotation;
using IIIF.Presentation.V3.Content;
using IIIF.Presentation.V3.Strings;
using Microsoft.Extensions.Options;
using Orchestrator.Settings;
using IIIF2 = IIIF.Presentation.V2;
using IIIF3 = IIIF.Presentation.V3;

namespace Orchestrator.Infrastructure.IIIF
{
    /// <summary>
    /// Canvas factory for creating Canvases from Assets items for IIIF Manifests. 
    /// </summary>
    public class IIIFCanvasFactory
    {
        private readonly IAssetPathGenerator assetPathGenerator;
        private readonly IThumbnailPolicyRepository thumbnailPolicyRepository;
        private readonly OrchestratorSettings orchestratorSettings;
        private readonly Dictionary<string, ThumbnailPolicy> thumbnailPolicies = new();

        public IIIFCanvasFactory(
            IAssetPathGenerator assetPathGenerator,
            IOptions<OrchestratorSettings> orchestratorSettings,
            IThumbnailPolicyRepository thumbnailPolicyRepository)
        {
            this.assetPathGenerator = assetPathGenerator;
            this.thumbnailPolicyRepository = thumbnailPolicyRepository;
            this.orchestratorSettings = orchestratorSettings.Value;
        }

        /// <summary>
        /// Generate IIIF V3 canvases for assets.
        /// </summary>
        public async Task<List<IIIF3.Canvas>> CreateV3Canvases(List<Asset> results,
            CustomerPathElement customerPathElement)
        {
            int counter = 0;
            var canvases = new List<IIIF3.Canvas>(results.Count);
            foreach (var i in results)
            {
                var fullyQualifiedImageId = GetFullyQualifiedId(i, customerPathElement);
                var canvasId = string.Concat(fullyQualifiedImageId, "/canvas/c/", ++counter);
                var thumbnailSizes = await GetThumbnailSizesForImage(i);

                var canvas = new IIIF3.Canvas
                {
                    Id = canvasId,
                    Label = new LanguageMap("en", $"Canvas {counter}"),
                    Width = i.Width,
                    Height = i.Height,
                    Items = new AnnotationPage
                    {
                        Id = $"{canvasId}/page",
                        Items = new PaintingAnnotation
                        {
                            Target = new IIIF3.Canvas { Id = canvasId },
                            Id = $"{canvasId}/page/image",
                            Body = new Image
                            {
                                Id = GetFullQualifiedImagePath(i, customerPathElement,
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
                        Id = GetFullQualifiedThumbPath(i, customerPathElement, thumbnailSizes.OpenThumbnails),
                        Format = "image/jpeg",
                        Service = GetImageServiceForThumbnail(i, customerPathElement,
                            thumbnailSizes.OpenThumbnails)
                    }.AsListOf<ExternalResource>();
                }

                canvases.Add(canvas);
            }

            return canvases;
        }

        /// <summary>
        /// Generate IIIF V2 canvases for assets.
        /// </summary>
        public async Task<List<IIIF2.Canvas>> CreateV2Canvases(List<Asset> results,
            CustomerPathElement customerPathElement)
        {
            int counter = 0;
            var canvases = new List<IIIF2.Canvas>(results.Count);
            foreach (var i in results)
            {
                var fullyQualifiedImageId = GetFullyQualifiedId(i, customerPathElement);
                var canvasId = string.Concat(fullyQualifiedImageId, "/canvas/c/", ++counter);
                var thumbnailSizes = await GetThumbnailSizesForImage(i);

                var canvas = new IIIF2.Canvas
                {
                    Id = canvasId,
                    Label = new MetaDataValue($"Canvas {counter}"),
                    Width = i.Width,
                    Height = i.Height,
                    Images = new ImageAnnotation
                    {
                        Id = string.Concat(fullyQualifiedImageId, "/imageanno/0"),
                        On = canvasId,
                        Resource = new IIIF2.ImageResource
                        {
                            Id = GetFullQualifiedImagePath(i, customerPathElement,
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
                        Id = GetFullQualifiedThumbPath(i, customerPathElement, thumbnailSizes.OpenThumbnails),
                        Service = GetImageServiceForThumbnail(i, customerPathElement, thumbnailSizes.OpenThumbnails)
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
                Id = GetFullQualifiedThumbServicePath(asset, customerPathElement),
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