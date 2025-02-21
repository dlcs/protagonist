using DLCS.Core.Types;

namespace Engine.Ingest.Image.Completion;

/// <summary>
/// Basic interface for triggering orchestration of an asset
/// </summary>
public interface IOrchestratorClient
{
    Task<bool> TriggerOrchestration(AssetId assetId);
}

/// <summary>
/// Implementation of <see cref="IOrchestratorClient"/> that triggers orchestration via info.json request
/// </summary>
public class InfoJsonOrchestratorClient : IOrchestratorClient
{
    private readonly HttpClient httpClient;
    private readonly ILogger<InfoJsonOrchestratorClient> logger;

    public InfoJsonOrchestratorClient(HttpClient httpClient, ILogger<InfoJsonOrchestratorClient> logger)
    {
        this.httpClient = httpClient;
        this.logger = logger;
    }

    public async Task<bool> TriggerOrchestration(AssetId assetId)
    {
        try
        {
            var path = GetOrchestrationPath(assetId);
            var response = await httpClient.GetAsync(path);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error orchestrating asset {AssetId} after ingestion", assetId);
            return false;
        }
    }
        
    private string GetOrchestrationPath(AssetId assetId)
        // /iiif-img/1/2/the-image/info.json
        => $"/iiif-img/{assetId}/info.json";
}