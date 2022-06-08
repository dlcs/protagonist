using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core;
using DLCS.Model.Assets;
using DLCS.Web.Requests.AssetDelivery;
using DLCS.Web.Response;
using IIIF;
using IIIF.ImageApi.V2;
using IIIF.Serialisation;
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
            
            var infoJson = InfoJsonBuilder.GetImageApi2_1Level1(imageId, asset.Width, asset.Height, asset.OpenThumbs);

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

        private async Task AddAuthServicesToInfoJson(ImageService2 infoJson, OrchestrationImage image)
        {
            var authService = await iiifAuthBuilder.GetAuthCookieServiceForAsset(image);
            if (authService != null)
            {
                infoJson.Service = new List<IService> { authService };
            }
        }
    }
}