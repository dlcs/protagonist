using DLCS.Core.Types;

namespace Engine.Ingest.Image.Completion;

public class OrchestratorClient
{
    private readonly HttpClient httpClient;
    private readonly ILogger<OrchestratorClient> logger;

    public OrchestratorClient(HttpClient httpClient, ILogger<OrchestratorClient> logger)
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