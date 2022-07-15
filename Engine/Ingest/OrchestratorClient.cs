using DLCS.Model.Assets;

namespace Engine.Ingest;

public class OrchestratorClient
{
    private readonly HttpClient httpClient;
    private readonly ILogger<OrchestratorClient> logger;

    public OrchestratorClient(HttpClient httpClient, ILogger<OrchestratorClient> logger)
    {
        this.httpClient = httpClient;
        this.logger = logger;
    }

    public async Task<bool> TriggerOrchestration(Asset asset)
    {
        try
        {
            var path = GetOrchestrationPath(asset);
            var response = await httpClient.GetAsync(path);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error orchestrating asset {AssetId} after ingestion", asset.Id);
            return false;
        }
    }
        
    private string GetOrchestrationPath(Asset assetId)
        // /iiif-img/1/2/the-image/info.json
        => $"/iiif-img/{assetId}/info.json";
}