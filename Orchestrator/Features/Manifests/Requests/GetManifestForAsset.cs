using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core;
using DLCS.Core.Collections;
using DLCS.Model.Assets;
using DLCS.Web.Requests.AssetDelivery;
using DLCS.Web.Response;
using IIIF;
using IIIF.ImageApi.Service;
using IIIF.Presentation.V2;
using IIIF.Presentation.V2.Annotation;
using IIIF.Presentation.V2.Strings;
using IIIF.Serialisation;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestrator.Infrastructure.Mediatr;
using Orchestrator.Models;
using Orchestrator.Settings;

namespace Orchestrator.Features.Manifests.Requests
{
    /// <summary>
    /// Mediatr request for generating basic single-item manifest for specified image
    /// </summary>
    public class GetManifestForAsset : IRequest<IIIFJsonResponse>, IGenericAssetRequest
    {
        public string FullPath { get; }
        
        public BaseAssetRequest AssetRequest { get; set; }

        public GetManifestForAsset(string path)
        {
            FullPath = path;
        }
    }
    
    public class GetManifestForAssetHandler : IRequestHandler<GetManifestForAsset, IIIFJsonResponse>
    {
        private readonly IAssetRepository assetRepository;
        private readonly IAssetPathGenerator assetPathGenerator;
        private readonly IThumbRepository thumbRepository;
        private readonly ILogger<GetManifestForAssetHandler> logger;
        private readonly OrchestratorSettings orchestratorSettings;

        public GetManifestForAssetHandler(
            IAssetRepository assetRepository,
            IAssetPathGenerator assetPathGenerator,
            IThumbRepository thumbRepository,
            IOptions<OrchestratorSettings> orchestratorSettings,
            ILogger<GetManifestForAssetHandler> logger)
        {
            this.assetRepository = assetRepository;
            this.assetPathGenerator = assetPathGenerator;
            this.thumbRepository = thumbRepository;
            this.orchestratorSettings = orchestratorSettings.Value;
            this.logger = logger;
        }

        public async Task<IIIFJsonResponse> Handle(GetManifestForAsset request, CancellationToken cancellationToken)
        {
            var assetId = request.AssetRequest.GetAssetId();
            var asset = await assetRepository.GetAsset(assetId);
            if (asset is not { Family: AssetFamily.Image })
            {
                logger.LogDebug("Request iiif-manifest for asset {AssetId} but is not found or not an image", assetId);
                return IIIFJsonResponse.Empty;
            }

            var openThumbs = await thumbRepository.GetOpenSizes(assetId);
            var manifest = GenerateV2Manifest(request.AssetRequest, asset, openThumbs);

            return IIIFJsonResponse.Open(manifest.AsJson());
        }

        private Manifest GenerateV2Manifest(BaseAssetRequest assetRequest, Asset asset, List<int[]>? openThumbs)
        {
            var fullyQualifiedImageId = GetFullyQualifiedId(assetRequest, orchestratorSettings.Proxy.ImagePath);
            var manifest = new Manifest
            {
                Id = fullyQualifiedImageId,
                Context = IIIF.Presentation.Context.V2,
                Metadata = new Metadata
                    {
                        Label = new MetaDataValue("origin"),
                        Value = new MetaDataValue(asset.Origin)
                    }
                    .AsList(),
                Sequences = new Sequence
                {
                    Id = string.Concat(fullyQualifiedImageId, "/sequence/s0"),
                    Label = new MetaDataValue("Sequence 0"),
                    ViewingHint = "paged",
                    Canvases = CreateCanvases(fullyQualifiedImageId, assetRequest, asset, openThumbs)
                }.AsList()
            };

            return manifest;
        }

        private List<Canvas> CreateCanvases(string fullyQualifiedImageId, BaseAssetRequest assetRequest, Asset asset, List<int[]>? openThumbs)
        {
            var fullyQualifiedThumbId = GetFullyQualifiedId(assetRequest, orchestratorSettings.Proxy.ThumbsPath);

            var imageExample = $"{fullyQualifiedImageId}/full/{asset.Width},{asset.Height}/0/default.jpg";

            var canvasId = string.Concat(fullyQualifiedImageId, "/canvas/c/0");
            var canvas = new Canvas
            {
                Id = canvasId,
                Label = new MetaDataValue($"Image - {assetRequest.GetAssetId()}"),
                Height = asset.Height,
                Width = asset.Width,
                Images = new ImageAnnotation
                {
                    Id = string.Concat(fullyQualifiedImageId, "/imageanno/0"),
                    On = canvasId,
                    Resource = new ImageResource
                    {
                        Id = imageExample,
                        Width = asset.Width,
                        Height = asset.Height,
                        Service = new ImageService2
                        {
                            Id = fullyQualifiedImageId,
                            Profile = ImageService2.Level1Profile,
                            Width = asset.Width,
                            Height = asset.Height,
                        }.AsListOf<IService>()
                    }
                }.AsList()
            };

            if (!openThumbs.IsNullOrEmpty())
            {
                var thumbsService = InfoJsonBuilder.GetImageApi2_1Level0(fullyQualifiedThumbId, openThumbs!);
                var smallestThumb = thumbsService.Sizes[0];

                string thumbExample =
                    $"{fullyQualifiedThumbId}/full/{smallestThumb.Width},{smallestThumb.Height}/0/default.jpg";
                
                canvas.Thumbnail = new Thumbnail
                {
                    Id = thumbExample,
                    Service = thumbsService.AsListOf<IService>() 
                }.AsList();
            }

            return canvas.AsList();
        }

        private string GetFullyQualifiedId(BaseAssetRequest baseAssetRequest, string prefix)
            => assetPathGenerator.GetFullPathForRequest(
                baseAssetRequest,
                (assetRequest, template) => DlcsPathHelpers.GeneratePathFromTemplate(
                    template,
                    prefix,
                    assetRequest.CustomerPathValue,
                    assetRequest.Space.ToString(),
                    assetRequest.AssetId));
    }
}