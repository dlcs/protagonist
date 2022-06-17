using System;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core;
using DLCS.Core.Types;
using DLCS.Web.Requests.AssetDelivery;
using DLCS.Web.Response;
using IIIF;
using IIIF.ImageApi.V2;
using IIIF.ImageApi.V3;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestrator.Assets;
using Orchestrator.Features.Images.ImageServer;
using Orchestrator.Features.Images.Orchestration;
using Orchestrator.Infrastructure.Auth;
using Orchestrator.Infrastructure.Mediatr;
using Orchestrator.Models;
using Orchestrator.Settings;
using Version = IIIF.ImageApi.Version;

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
        private readonly IOrchestrationQueue orchestrationQueue;
        private readonly IAssetAccessValidator accessValidator;
        private readonly InfoJsonService infoJsonService;
        private readonly ILogger<GetImageInfoJsonHandler> logger;
        private readonly OrchestratorSettings orchestratorSettings;

        public GetImageInfoJsonHandler(
            IAssetTracker assetTracker,
            IAssetPathGenerator assetPathGenerator,
            IOrchestrationQueue orchestrationQueue,
            IAssetAccessValidator accessValidator,
            IOptions<OrchestratorSettings> orchestratorSettings,
            InfoJsonService infoJsonService,
            ILogger<GetImageInfoJsonHandler> logger)
        {
            this.assetTracker = assetTracker;
            this.assetPathGenerator = assetPathGenerator;
            this.orchestrationQueue = orchestrationQueue;
            this.accessValidator = accessValidator;
            this.infoJsonService = infoJsonService;
            this.logger = logger;
            this.orchestratorSettings = orchestratorSettings.Value;
        }
        
        public async Task<DescriptionResourceResponse> Handle(GetImageInfoJson request, CancellationToken cancellationToken)
        {
            var assetId = request.AssetRequest.GetAssetId();

            if (!IsRequestedVersionSupportedByImageServer(assetId, request.Version))
            {
                return DescriptionResourceResponse.BadRequest();
            }

            var asset = await assetTracker.GetOrchestrationAsset<OrchestrationImage>(assetId);
            if (asset == null)
            {
                return DescriptionResourceResponse.Empty;
            }
            
            var infoJsonResponse =
                await infoJsonService.GetInfoJson(asset, request.Version, CancellationToken.None);
            if (infoJsonResponse == null)
            {
                return DescriptionResourceResponse.Empty;
            }

            var infoJson = infoJsonResponse.InfoJson;
            SetIdProperty(request, infoJson);

            // TODO - handle err codes

            ValueTask orchestrationTask = infoJsonResponse.WasOrchestrated
                ? ValueTask.CompletedTask
                : DoOrchestrationIfRequired(asset, request.NoOrchestrationOverride, cancellationToken);
            
            if (!asset.RequiresAuth)
            {
                await orchestrationTask;
                return DescriptionResourceResponse.Open(infoJson);
            }

            var accessResult =
                await accessValidator.TryValidate(assetId.Customer, asset.Roles, AuthMechanism.BearerToken);
            await orchestrationTask;
            return accessResult == AssetAccessResult.Authorized
                ? DescriptionResourceResponse.Restricted(infoJson)
                : DescriptionResourceResponse.Unauthorised(infoJson);
        }

        private bool IsRequestedVersionSupportedByImageServer(AssetId assetId, Version version)
        {
            var targetPath = orchestratorSettings.GetImageServerPath(assetId, version);
            return !string.IsNullOrEmpty(targetPath);
        }

        private ValueTask DoOrchestrationIfRequired(OrchestrationImage orchestrationImage, bool noOrchestrationOverride,
            CancellationToken cancellationToken)
        {
            if (noOrchestrationOverride || !orchestratorSettings.OrchestrateOnInfoJson)
            {
                return ValueTask.CompletedTask;
            }

            logger.LogDebug("Info.json queueing orchestration for asset {Asset}", orchestrationImage.AssetId);
            return orchestrationQueue.QueueRequest(orchestrationImage, cancellationToken);
        }
        
        private void SetIdProperty(GetImageInfoJson request, JsonLdBase infoJson)
        {
            // The Id property can differ depending on config (DefaultIIIFImageVersion) or hostname if serving
            // via a cdn so simplest option is to rewrite it on way out.
            switch (infoJson)
            {
                case ImageService2 imageService2:
                    imageService2.Id = GetImageId(request);
                    break;
                case ImageService3 imageService3:
                    imageService3.Id = GetImageId(request);
                    break;
                default:
                    throw new InvalidOperationException("Info.json is unknown version");
            }
        }

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
    }
}