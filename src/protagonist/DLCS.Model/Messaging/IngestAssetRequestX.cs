using DLCS.Model.Assets;

namespace DLCS.Model.Messaging;

/// <summary>
/// Extension methods for asset ingest requests.
/// </summary>
public static class IngestAssetRequestX
{
    public static IngestAssetRequest GetMinimalPayload(this IngestAssetRequest ingestAssetRequest)
    {
        return new IngestAssetRequest(new Asset() { Id = ingestAssetRequest.Asset.Id }, ingestAssetRequest.Created);
    }
}