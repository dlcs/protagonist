using System.Net;
using System.Threading.Tasks;
using DLCS.Model.Assets;

namespace DLCS.Model.Messaging
{
    public interface IMessageBus
    {
        Task SendIngestAssetRequest(IngestAssetRequest ingestAssetRequest);
        Task<HttpStatusCode> SendImmediateIngestAssetRequest(IngestAssetRequest ingestAssetRequest, bool derivativesOnly);
        Task SendAssetModifiedNotification(Asset before, Asset after);
    }
}