using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.Core;
using DLCS.Core.Strings;
using DLCS.Core.Types;
using DLCS.Web.Requests.AssetDelivery;
using DLCS.Web.Response;
using IIIF;
using IIIF.ImageApi.V2;
using IIIF.ImageApi.V3;
using IIIF.Serialisation;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestrator.Assets;
using Orchestrator.Features.Images.Orchestration;
using Orchestrator.Infrastructure.Auth;
using Orchestrator.Infrastructure.IIIF;
using Orchestrator.Infrastructure.Mediatr;
using Orchestrator.Infrastructure.ReverseProxy;
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
            
            var imageId = GetImageId(request);
            var infoJsonResponse =
                await infoJsonService.GetInfoJson(asset, imageId, request.Version, CancellationToken.None);
            if (infoJsonResponse == null)
            {
                return DescriptionResourceResponse.Empty;
            }
            
            // TODO - handle null and err

            ValueTask orchestrationTask = infoJsonResponse.WasOrchestrated
                ? ValueTask.CompletedTask
                : DoOrchestrationIfRequired(asset, request.NoOrchestrationOverride, cancellationToken);
            
            if (!asset.RequiresAuth)
            {
                await orchestrationTask;
                return DescriptionResourceResponse.Open(infoJsonResponse.InfoJson);
            }

            var accessResult =
                await accessValidator.TryValidate(assetId.Customer, asset.Roles, AuthMechanism.BearerToken);
            await orchestrationTask;
            return accessResult == AssetAccessResult.Authorized
                ? DescriptionResourceResponse.Restricted(infoJsonResponse.InfoJson)
                : DescriptionResourceResponse.Unauthorised(infoJsonResponse.InfoJson);
        }

        private bool IsRequestedVersionSupportedByImageServer(AssetId assetId, Version version)
        {
            var targetPath = orchestratorSettings.GetImageServerPath(assetId, version);
            return string.IsNullOrEmpty(targetPath);
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

    /// <summary>
    /// Service for managing the fetching and storing of info.json requests.
    /// </summary>
    public class InfoJsonService
    {
        private readonly IStorageKeyGenerator storageKeyGenerator;
        private readonly IBucketReader bucketReader;
        private readonly IBucketWriter bucketWriter;
        private readonly InfoJsonConstructor infoJsonConstructor;
        private readonly IOptions<OrchestratorSettings> orchestratorSettings;
        private readonly ILogger<InfoJsonService> logger;

        public InfoJsonService(
            IStorageKeyGenerator storageKeyGenerator,
            IBucketReader bucketReader,
            IBucketWriter bucketWriter,
            InfoJsonConstructor infoJsonConstructor,
            IOptions<OrchestratorSettings> orchestratorSettings,
            ILogger<InfoJsonService> logger)
        {
            this.storageKeyGenerator = storageKeyGenerator;
            this.bucketReader = bucketReader;
            this.bucketWriter = bucketWriter;
            this.infoJsonConstructor = infoJsonConstructor;
            this.orchestratorSettings = orchestratorSettings;
            this.logger = logger;
        }

        public async Task<InfoJsonResponse?> GetInfoJson(
            OrchestrationImage orchestrationImage,
            string imageId,
            IIIF.ImageApi.Version version,
            CancellationToken cancellationToken = default)
        {
            var infoJsonCandidate = GetInfoJsonKey(orchestrationImage, version);
            var infoJson = await GetStoredInfoJson(infoJsonCandidate, cancellationToken);

            if (infoJson != null && infoJson != Stream.Null)
            {
                // If info.json found in S3, return it
                JsonLdBase deserialisedInfoJson = version == Version.V2
                    ? infoJson.FromJsonStream<ImageService2>()
                    : infoJson.FromJsonStream<ImageService3>();
                logger.LogDebug("Found info.json version {Version} for {AssetId}", version, orchestrationImage.AssetId);
                return new InfoJsonResponse(deserialisedInfoJson, false);
            }

            // If not found, build new copy
            var infoJsonResponse = await infoJsonConstructor.BuildInfoJsonFromImageServer(orchestrationImage, imageId, 
                version, cancellationToken);

            if (infoJsonResponse == null) return null;
            
            await StoreInfoJson(infoJsonResponse, orchestrationImage, version, cancellationToken);
            return new InfoJsonResponse(infoJsonResponse, true);
        }

        private ObjectInBucket GetInfoJsonKey(OrchestrationImage asset, Version version)
        {
            var imageServer = orchestratorSettings.Value.ImageServer.ToString();
            var infoJsonCandidate = storageKeyGenerator.GetInfoJsonLocation(asset.AssetId, imageServer, version);
            return infoJsonCandidate;
        }

        private async Task<Stream?> GetStoredInfoJson(ObjectInBucket infoJsonKey, CancellationToken cancellationToken) 
            => await bucketReader.GetObjectContentFromBucket(infoJsonKey, cancellationToken);
        
        private async Task StoreInfoJson(JsonLdBase infoJson, OrchestrationImage orchestrationImage,
            Version version, CancellationToken cancellationToken)
        {
            // Write this to the bucket
            var bucket = storageKeyGenerator.GetInfoJsonLocation(orchestrationImage.AssetId,
                orchestratorSettings.Value.ImageServer.ToString(), version);
            await bucketWriter.WriteToBucket(bucket, infoJson.AsJson(), "application/json", cancellationToken);
        }
    }

    /// <summary>
    /// Service responsible for orchestrating image, calling IImageServerClient to get info.json and update with
    /// required information that image-server will be unaware of (e.g. Auth, Id).
    /// </summary>
    public class InfoJsonConstructor
    {
        private readonly IIIFAuthBuilder iiifAuthBuilder;
        private readonly IImageServerClient imageServerClient;

        public InfoJsonConstructor(
            IIIFAuthBuilder iiifAuthBuilder,
            IImageServerClient imageServerClient)
        {
            this.iiifAuthBuilder = iiifAuthBuilder;
            this.imageServerClient = imageServerClient;
        }

        public async Task<JsonLdBase?> BuildInfoJsonFromImageServer(OrchestrationImage orchestrationImage,
            string infoJsonPath,
            IIIF.ImageApi.Version version,
            CancellationToken cancellationToken = default)
        {
            // Get info.json from downstream image server and add related services to it
            // TODO - handle 501 etc from downstream image-server
            if (version == Version.V2)
            {
                var imageServer2 =
                    await imageServerClient.GetInfoJson<ImageService2>(orchestrationImage, version, cancellationToken);
                await UpdateImageService(imageServer2, orchestrationImage, infoJsonPath, cancellationToken);
                return imageServer2;
            }
            else
            {
                var imageServer3 =
                    await imageServerClient.GetInfoJson<ImageService3>(orchestrationImage, version, cancellationToken);
                await UpdateImageService(imageServer3, orchestrationImage, infoJsonPath, cancellationToken);
                return imageServer3;
            }
        }

        private async Task UpdateImageService(ImageService2? imageService, OrchestrationImage orchestrationImage, 
            string infoJsonPath, CancellationToken cancellationToken)
        {
            if (imageService == null) return;
            
            imageService.Id = infoJsonPath;

            if (orchestrationImage.RequiresAuth)
            {
                imageService.Service ??= new List<IService>(1);
                var authCookieServiceForAsset =
                    await iiifAuthBuilder.GetAuthCookieServiceForAsset(orchestrationImage, cancellationToken);
                imageService.Service.Add(authCookieServiceForAsset);
            }
        }

        private async Task UpdateImageService(ImageService3? imageService, OrchestrationImage orchestrationImage, 
            string infoJsonPath, CancellationToken cancellationToken)
        {
            if (imageService == null) return;
            
            imageService.Id = infoJsonPath;

            if (orchestrationImage.RequiresAuth)
            {
                imageService.Service ??= new List<IService>(1);
                var authCookieServiceForAsset =
                    await iiifAuthBuilder.GetAuthCookieServiceForAsset(orchestrationImage, cancellationToken);
                imageService.Service.Add(authCookieServiceForAsset);
            }
        }
    }

    /// <summary>
    /// Basic http client for making requests to image-servers
    /// </summary>
    public interface IImageServerClient
    {
        Task<TImageService?> GetInfoJson<TImageService>(OrchestrationImage orchestrationImage,
            IIIF.ImageApi.Version version,
            CancellationToken cancellationToken = default)
            where TImageService : JsonLdBase;
    }

    /// <summary>
    /// Implementation of <see cref="IImageServerClient"/> that uses Yarp config to derive destination addresses 
    /// </summary>
    public class YarpImageServerClient : IImageServerClient
    {
        private readonly HttpClient httpClient;
        private readonly DownstreamDestinationSelector downstreamDestinationSelector;
        private readonly IOptions<OrchestratorSettings> orchestratorSettings;
        private readonly ILogger<YarpImageServerClient> logger;
        private readonly IImageOrchestrator orchestrator;

        public YarpImageServerClient(
            HttpClient httpClient,
            DownstreamDestinationSelector downstreamDestinationSelector,
            IOptions<OrchestratorSettings> orchestratorSettings,
            ILogger<YarpImageServerClient> logger,
            IImageOrchestrator orchestrator)
        {
            this.httpClient = httpClient;
            this.downstreamDestinationSelector = downstreamDestinationSelector;
            this.orchestratorSettings = orchestratorSettings;
            this.logger = logger;
            this.orchestrator = orchestrator;
        }

        public async Task<TImageService?> GetInfoJson<TImageService>(OrchestrationImage orchestrationImage,
            IIIF.ImageApi.Version version,
            CancellationToken cancellationToken = default)
            where TImageService : JsonLdBase
        {
            // Orchestrate the image to verify that image-server will be able to generate an info.json 
            await orchestrator.OrchestrateImage(orchestrationImage, cancellationToken);
            
            var imageServerPath = GetInfoJsonPath(orchestrationImage, version);
            if (string.IsNullOrEmpty(imageServerPath)) return null;

            try
            {
                var infoJson = await httpClient.GetStringAsync(imageServerPath, cancellationToken);
                return infoJson.FromJson<TImageService>();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting info.json from {ImageServerPath}", imageServerPath);
                throw;
            }
        }

        private string? GetInfoJsonPath(OrchestrationImage orchestrationImage, Version version)
        {
            var settings = orchestratorSettings.Value;
            var imageServerAddress =
                downstreamDestinationSelector.GetRandomDestinationAddress(ProxyDestination.ImageServer);
            if (string.IsNullOrEmpty(imageServerAddress))
            {
                logger.LogInformation("No destination image-server found for {Version}", settings.ImageServer);
                return null;
            }
            
            var targetPath = settings.GetImageServerPath(orchestrationImage.AssetId, version);
            if (string.IsNullOrEmpty(targetPath))
            {
                logger.LogInformation("No target image-server found for {ImageServer}, {Version}", settings.ImageServer,
                    version);
                return null;
            }
            
            // Get full info.json path for downstream image server
            var imageServerPath = imageServerAddress.ToConcatenated('/', targetPath, "info.json");
            return imageServerPath;
        }
    }

    public class InfoJsonResponse
    {
        public JsonLdBase InfoJson { get; }
        public bool WasOrchestrated { get; }

        public InfoJsonResponse(JsonLdBase infoJson, bool wasOrchestrated)
        {
            InfoJson = infoJson;
            WasOrchestrated = wasOrchestrated;
        }
    }
}