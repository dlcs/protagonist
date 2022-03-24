using DLCS.Model.Assets;

namespace DLCS.Model.Messaging
{
    public interface IMessageBus
    {
        void SendIngestAssetRequest(IngestAssetRequest ingestAssetRequest);

        void SendAssetModifiedNotification(Asset before, Asset after);
    }
}