using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core;
using DLCS.Core.Collections;
using DLCS.Core.Strings;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Auth;
using DLCS.Model.Auth.Entities;
using DLCS.Web.Requests.AssetDelivery;
using DLCS.Web.Response;
using IIIF.Serialisation;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Orchestrator.Assets;
using Orchestrator.Features.Images.Orchestration;
using Orchestrator.Infrastructure.Auth;
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
        private readonly IAuthServicesRepository authServicesRepository;
        private readonly IOrchestrationQueue orchestrationQueue;
        private readonly IAssetAccessValidator accessValidator;
        private readonly ILogger<GetImageInfoJsonHandler> logger;
        private readonly OrchestratorSettings orchestratorSettings;

        public GetImageInfoJsonHandler(
            IAssetTracker assetTracker,
            IAssetPathGenerator assetPathGenerator,
            IAuthServicesRepository authServicesRepository,
            IOrchestrationQueue orchestrationQueue,
            IAssetAccessValidator accessValidator,
            IOptions<OrchestratorSettings> orchestratorSettings,
            ILogger<GetImageInfoJsonHandler> logger)
        {
            this.assetTracker = assetTracker;
            this.assetPathGenerator = assetPathGenerator;
            this.authServicesRepository = authServicesRepository;
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

            if (!asset.RequiresAuth)
            {
                // TODO - update to use JsonLdBase rather than just a string
                var infoJson =
                    InfoJsonBuilder.GetImageApi2_1Level1(imageId, asset.Width, asset.Height, asset.OpenThumbs);
                await orchestrationTask;
                return DescriptionResourceResponse.Open(infoJson.AsJson());
            }

            var accessResult =
                await accessValidator.TryValidate(assetId.Customer, asset.Roles, AuthMechanism.BearerToken);
            var authInfoJson = await GetAuthInfoJson(imageId, asset, assetId);
            await orchestrationTask;
            return accessResult == AssetAccessResult.Authorized
                ? DescriptionResourceResponse.Restricted(authInfoJson)
                : DescriptionResourceResponse.Unauthorised(authInfoJson);
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
                        baseAssetRequest.RoutePrefix,
                        baseAssetRequest.CustomerPathValue,
                        baseAssetRequest.Space.ToString(),
                        baseAssetRequest.AssetId);
                });

        private async Task<string> GetAuthInfoJson(string imageId, OrchestrationImage asset, AssetId assetId)
        {
            var authServices = await GetAuthServices(assetId, asset.Roles);
            if (authServices.IsNullOrEmpty())
            {
                logger.LogWarning("Unable to get auth services for {Asset}", assetId);
                return InfoJsonBuilder.GetImageApi2_1Level1(imageId, asset.Width, asset.Height, asset.OpenThumbs)
                    .AsJson();
            }
            var infoJsonServices = GenerateInfoJsonServices(assetId, authServices);
            return InfoJsonBuilder.GetImageApi2_1Level1Auth(imageId, asset.Width, asset.Height, asset.OpenThumbs,
                infoJsonServices);
        }

        private string GenerateInfoJsonServices(AssetId assetId, List<AuthService>? authServices)
        {
            // TODO - fix this with IIIF nuget lib, this is lift + shift from Deliverator
            var authServicesUriFormat = orchestratorSettings.AuthServicesUriTemplate;
            var id = authServicesUriFormat
                .Replace("{customer}", assetId.Customer.ToString()) // should this be customer Path value?
                .Replace("{behaviour}", authServices[0].Name);
            var presentationObject = new JObject
            {
                { "@id", id },
                { "profile", authServices[0].Profile },
                { "label", authServices[0].Label },
                { "description", authServices[0].Description }
            };
            
            if (authServices.Count > 1)
            {
                AddAuthServices(assetId, authServices, presentationObject, authServicesUriFormat);
            }

            return presentationObject.ToString(Formatting.None);
        }

        private async Task<List<AuthService>> GetAuthServices(AssetId assetId, IEnumerable<string> rolesList)
        {
            var authServices = new List<AuthService>();
            foreach (var role in rolesList)
            {
                authServices.AddRange(await authServicesRepository.GetAuthServicesForRole(assetId.Customer, role));
            }

            return authServices;
        }
        
        private static void AddAuthServices(AssetId assetId, List<AuthService> authServices, 
            JObject presentationObject, string authServicesUriFormat)
        {
            var subServices = new List<JObject>(authServices.Count - 1);
            JObject subService;
            foreach (var authService in authServices.Skip(1))
            {
                if (authService.Profile == Constants.ProfileV1.Logout ||
                    authService.Profile == Constants.ProfileV0.Logout)
                {
                    subService = new JObject
                    {
                        { "@id", string.Concat(presentationObject["@id"], "/logout") },
                        { "profile", authService.Profile }
                    };
                }
                else if (authService.Profile == Constants.ProfileV1.Token ||
                         authService.Profile == Constants.ProfileV0.Token)
                {
                    var tokenServiceUri = authServicesUriFormat
                        .Replace("{customer}", assetId.Customer.ToString())
                        .Replace("{behaviour}", authService.Name);
                    
                    subService = new JObject
                    {
                        {"@id", tokenServiceUri},
                        {"profile", authService.Profile}
                    };
                }
                else
                {
                    throw new ArgumentException($"Unknown AuthService profile type: {authService.Profile}");
                }
                
                if (authService.Label.HasText())
                {
                    subService.Add("label", authService.Label);
                }
                if (authService.Description.HasText())
                {
                    subService.Add("description", authService.Description);
                }
                
                subServices.Add(subService);
            }

            presentationObject["service"] = new JArray(subServices.ToArray());
        }
    }
}