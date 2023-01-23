using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Types;
using DLCS.Web.Requests.AssetDelivery;
using DLCS.Web.Response;
using IIIF;
using IIIF.Auth.V1;
using IIIF.ImageApi.V2;
using IIIF.ImageApi.V3;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestrator.Assets;
using Orchestrator.Features.Auth.Paths;
using Orchestrator.Features.Images.ImageServer;
using Orchestrator.Features.Images.Orchestration;
using Orchestrator.Infrastructure.Auth;
using Orchestrator.Infrastructure.Mediatr;
using Orchestrator.Models;
using Orchestrator.Settings;
using Version = IIIF.ImageApi.Version;

namespace Orchestrator.Features.Images.Requests;

/// <summary>
/// Mediatr request for generating info.json request for specified image.
/// </summary>
public class GetImageInfoJson : IRequest<DescriptionResourceResponse>, IImageRequest
{
    public string FullPath { get; }
    public bool NoOrchestrationOverride { get; }
    public Version Version { get; }

    public ImageAssetDeliveryRequest AssetRequest { get; set; }

    public GetImageInfoJson(string path, Version version, bool noOrchestrationOverride)
    {
        FullPath = path;
        Version = version;
        NoOrchestrationOverride = noOrchestrationOverride;
    }
}

public class GetImageInfoJsonHandler : IRequestHandler<GetImageInfoJson, DescriptionResourceResponse>
{
    private readonly IAssetTracker assetTracker;
    private readonly IAuthPathGenerator authPathGenerator;
    private readonly IAssetPathGenerator assetPathGenerator;
    private readonly IOrchestrationQueue orchestrationQueue;
    private readonly IAssetAccessValidator accessValidator;
    private readonly InfoJsonService infoJsonService;
    private readonly ILogger<GetImageInfoJsonHandler> logger;
    private readonly OrchestratorSettings orchestratorSettings;

    public GetImageInfoJsonHandler(
        IAssetTracker assetTracker,
        IAssetPathGenerator assetPathGenerator,
        IAuthPathGenerator authPathGenerator,
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
        this.authPathGenerator = authPathGenerator;
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
        SetIdProperties(request, infoJson);

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
    
    private void SetIdProperties(GetImageInfoJson request, JsonLdBase infoJson)
    {
        // The Id properties can differ depending on config (DefaultIIIFImageVersion) or hostname if serving
        // via a cdn so simplest option is to rewrite it on way out.
        switch (infoJson)
        {
            case ImageService2 imageService2:
                imageService2.Id = GetImageId(request);
                SetServiceIdProperties(request.AssetRequest.GetAssetId(), imageService2.Service);
                break;
            case ImageService3 imageService3:
                imageService3.Id = GetImageId(request);
                SetServiceIdProperties(request.AssetRequest.GetAssetId(), imageService3.Service);
                break;
            default:
                throw new InvalidOperationException("Info.json is unknown version");
        }
    }

    private string GetImageId(GetImageInfoJson request)
    {
        var baseRequest = request.AssetRequest.CloneBasicPathElements();
        
        // We want the image id only, without "/info.json"
        baseRequest.AssetPath = request.AssetRequest.AssetId;
        return assetPathGenerator.GetFullPathForRequest(baseRequest);
    }

    private void SetServiceIdProperties(AssetId assetId, List<IService>? services)
    {
        void SetAuthId(IService service)
        {
            service.Id =
                authPathGenerator.GetAuthPathForRequest(assetId.Customer.ToString(), service.Id ?? "_unknown_");
        }
        
        foreach (var service in services ?? Enumerable.Empty<IService>())
        {
            if (service == null) continue;
            if (service is AuthCookieService cookieService)
            {
                SetAuthId(cookieService);
                SetServiceIdProperties(assetId, cookieService.Service);
            }
            else if (service is AuthLogoutService or AuthTokenService)
            {
                SetAuthId(service);
            }
            else
            {
                logger.LogWarning(
                    "Encountered unknown service type on info.json, unable to update Id '{ServiceType}'",
                    service.GetType());
            }
        }
    }
}