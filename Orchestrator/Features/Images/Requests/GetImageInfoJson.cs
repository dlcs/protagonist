using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core;
using DLCS.Model.Assets;
using DLCS.Web.Requests.AssetDelivery;
using DLCS.Web.Response;
using IIIF;
using IIIF.ImageApi;
using IIIF.ImageApi.V2;
using IIIF.ImageApi.V3;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestrator.Assets;
using Orchestrator.Features.Images.Orchestration;
using Orchestrator.Infrastructure.Auth;
using Orchestrator.Infrastructure.IIIF;
using Orchestrator.Infrastructure.Mediatr;
using Orchestrator.Models;
using Orchestrator.Settings;

namespace Orchestrator.Features.Images.Requests
{
    /// <summary>
    /// Mediatr request for generating info.json request for specified image.
    /// </summary>
    public class GetImageInfoJson : IRequest<DescriptionResourceResponse>, IImageRequest
    {
        public string FullPath { get; }
        public bool NoOrchestrationOverride { get; }
        public IIIF.ImageApi.Version Version { get; }

        public ImageAssetDeliveryRequest AssetRequest { get; set; }

        public GetImageInfoJson(string path, IIIF.ImageApi.Version version, bool noOrchestrationOverride)
        {
            FullPath = path;
            Version = version;
            NoOrchestrationOverride = noOrchestrationOverride;
        }
    }

    public class GetImageInfoJsonHandler : IRequestHandler<GetImageInfoJson, DescriptionResourceResponse>
    {
        private readonly IAssetTracker assetTracker;
        private readonly IAssetPathGenerator assetPathGenerator;
        private readonly IIIFAuthBuilder iiifAuthBuilder;
        private readonly IOrchestrationQueue orchestrationQueue;
        private readonly IAssetAccessValidator accessValidator;
        private readonly ILogger<GetImageInfoJsonHandler> logger;
        private readonly OrchestratorSettings orchestratorSettings;

        public GetImageInfoJsonHandler(
            IAssetTracker assetTracker,
            IAssetPathGenerator assetPathGenerator,
            IIIFAuthBuilder iiifAuthBuilder,
            IOrchestrationQueue orchestrationQueue,
            IAssetAccessValidator accessValidator,
            IOptions<OrchestratorSettings> orchestratorSettings,
            ILogger<GetImageInfoJsonHandler> logger)
        {
            this.assetTracker = assetTracker;
            this.assetPathGenerator = assetPathGenerator;
            this.iiifAuthBuilder = iiifAuthBuilder;
            this.orchestrationQueue = orchestrationQueue;
            this.accessValidator = accessValidator;
            this.logger = logger;
            this.orchestratorSettings = orchestratorSettings.Value;
        }
        
        public async Task<DescriptionResourceResponse> Handle(GetImageInfoJson request, CancellationToken cancellationToken)
        {
            var assetId = request.AssetRequest.GetAssetId();
            var asset = await assetTracker.GetOrchestrationAsset<OrchestrationImage>(assetId);
            if (asset == null)
            {
                return DescriptionResourceResponse.Empty;
            }

            var orchestrationTask =
                DoOrchestrationIfRequired(asset, request.NoOrchestrationOverride, cancellationToken);

            var imageId = GetImageId(request);
            
            var infoJson = GetInfoJson(imageId, asset, request.Version);

            if (!asset.RequiresAuth)
            {
                await orchestrationTask;
                return DescriptionResourceResponse.Open(infoJson);
            }

            var accessResult =
                await accessValidator.TryValidate(assetId.Customer, asset.Roles, AuthMechanism.BearerToken);
            await AddAuthServicesToInfoJson(infoJson, asset);
            await orchestrationTask;
            return accessResult == AssetAccessResult.Authorized
                ? DescriptionResourceResponse.Restricted(infoJson)
                : DescriptionResourceResponse.Unauthorised(infoJson);
        }

        private ValueTask DoOrchestrationIfRequired(OrchestrationImage orchestrationImage, bool noOrchestrationOverride,
            CancellationToken cancellationToken)
        {
            if (noOrchestrationOverride || !orchestratorSettings.OrchestrateOnInfoJson)
            {
                return ValueTask.CompletedTask;
            }

            logger.LogDebug("Info.json starting orchestration for asset {Asset}", orchestrationImage.AssetId);
            return orchestrationQueue.QueueRequest(orchestrationImage, cancellationToken);
        }

        // TODO this is returning ImageApi 3 level 0 for now - next ticket will properly implement this to use
        // downstream image-server to generate info.json 
        private JsonLdBase GetInfoJson(string imageId, OrchestrationImage asset, Version version)
            => version == Version.V2
                ? InfoJsonBuilder.GetImageApi2_1Level1(imageId, asset.Width, asset.Height, asset.OpenThumbs)
                : InfoJsonBuilder.GetImageApi3_Level0(imageId, asset.OpenThumbs, asset.Width, asset.Height);

        private string GetImageId(GetImageInfoJson request)
            => assetPathGenerator.GetFullPathForRequest(
                request.AssetRequest,
                (assetRequest, template) =>
                {
                    var baseAssetRequest = assetRequest as BaseAssetRequest;
                    return DlcsPathHelpers.GeneratePathFromTemplate(
                        template,
                        baseAssetRequest.VersionedRoutePrefix,
                        baseAssetRequest.CustomerPathValue,
                        baseAssetRequest.Space.ToString(),
                        baseAssetRequest.AssetId);
                });

        private async Task AddAuthServicesToInfoJson(JsonLdBase infoJson, OrchestrationImage image)
        {
            var authService = await iiifAuthBuilder.GetAuthCookieServiceForAsset(image);
            if (authService != null)
            {
                switch (infoJson)
                {
                    case ImageService3 imageService3:
                        imageService3.Service = new List<IService> { authService };
                        break;
                    case ImageService2 imageService2:
                        imageService2.Service = new List<IService> { authService };
                        break;
                }
            }
        }
    }
}