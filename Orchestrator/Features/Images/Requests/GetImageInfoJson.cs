﻿using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core;
using DLCS.Core.Collections;
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

        public ImageAssetDeliveryRequest AssetRequest { get; set; }

        public GetImageInfoJson(string path, bool noOrchestrationOverride)
        {
            FullPath = path;
            NoOrchestrationOverride = noOrchestrationOverride;
        }
    }

    public class GetImageInfoJsonHandler : IRequestHandler<GetImageInfoJson, DescriptionResourceResponse>
    {
        private readonly IAssetTracker assetTracker;
        private readonly IAssetPathGenerator assetPathGenerator;
        private readonly IAuthServicesRepository authServicesRepository;
        private readonly IImageOrchestrator orchestrator;
        private readonly IAssetAccessValidator accessValidator;
        private readonly ILogger<GetImageInfoJsonHandler> logger;
        private readonly OrchestratorSettings orchestratorSettings;

        public GetImageInfoJsonHandler(
            IAssetTracker assetTracker,
            IAssetPathGenerator assetPathGenerator,
            IAuthServicesRepository authServicesRepository,
            IImageOrchestrator orchestrator,
            IAssetAccessValidator accessValidator,
            IOptions<OrchestratorSettings> orchestratorSettings,
            ILogger<GetImageInfoJsonHandler> logger)
        {
            this.assetTracker = assetTracker;
            this.assetPathGenerator = assetPathGenerator;
            this.authServicesRepository = authServicesRepository;
            this.orchestrator = orchestrator;
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

            var accessResult = await accessValidator.TryValidateBearerToken(assetId.Customer, asset.Roles);
            var authInfoJson = await GetAuthInfoJson(imageId, asset, assetId);
            await orchestrationTask;
            return accessResult == AssetAccessResult.Authorized
                ? DescriptionResourceResponse.Restricted(authInfoJson)
                : DescriptionResourceResponse.Unauthorised(authInfoJson);
        }

        private Task DoOrchestrationIfRequired(OrchestrationImage orchestrationImage, bool noOrchestrationOverride,
            CancellationToken cancellationToken)
        {
            if (noOrchestrationOverride || !orchestratorSettings.OrchestrateOnInfoJson)
            {
                return Task.CompletedTask;
            }

            logger.LogDebug("Info.json starting orchestration for asset {Asset}", orchestrationImage.AssetId);
            return orchestrator.OrchestrateImage(orchestrationImage, cancellationToken);
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
                presentationObject["service"] = new JArray(
                    new JObject
                    {
                        { "@id", string.Concat(presentationObject["@id"], "/", authServices[1].Name) },
                        { "profile", authServices[1].Profile }
                    },
                    new JObject
                    {
                        {
                            "@id", authServicesUriFormat
                                .Replace("{customer}", assetId.Customer.ToString())
                                .Replace("{behaviour}", "token")
                        },
                        { "profile", "http://iiif.io/api/auth/0/token" }
                    });
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
    }
}