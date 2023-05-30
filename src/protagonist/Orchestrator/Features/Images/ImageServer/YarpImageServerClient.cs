using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Strings;
using IIIF;
using IIIF.Serialisation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestrator.Assets;
using Orchestrator.Features.Images.Orchestration;
using Orchestrator.Infrastructure.ReverseProxy;
using Orchestrator.Settings;
using Version = IIIF.ImageApi.Version;

namespace Orchestrator.Features.Images.ImageServer;

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
        var imageServerPath = GetInfoJsonPath(orchestrationImage, version);
        if (string.IsNullOrEmpty(imageServerPath)) return null;
        if (orchestrationImage.IsNotFound()) return null;
        
        // Orchestrate the image to verify that image-server will be able to generate an info.json 
        var orchestrationResult = await orchestrator.EnsureImageOrchestrated(orchestrationImage, cancellationToken);

        if (orchestrationResult == OrchestrationResult.NotFound) return null;
        if (orchestrationResult == OrchestrationResult.Error)
        {
            logger.LogError("Error getting info.json for {AssetId}, error Orchestrating", orchestrationImage.AssetId);
            return null;
        }
            
        try
        {
            logger.LogTrace("Getting info.json for {AssetId} from image-server", orchestrationImage.AssetId);
            await using var infoJson = await httpClient.GetStreamAsync(imageServerPath, cancellationToken);
            return infoJson.FromJsonStream<TImageService>();
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