using DLCS.Model.Assets;

namespace DLCS.Model.Messaging;

public static class IngestAssetRequestX
{
    public static IngestAssetRequest GetMinimalPayload(this IngestAssetRequest ingestAssetRequest)
    {
        return new IngestAssetRequest(new Asset() { Id = ingestAssetRequest.Asset.Id }, ingestAssetRequest.Created);
    }
}