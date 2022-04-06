using DLCS.Model.Assets;
using DLCS.Model.Messaging;
using Microsoft.Extensions.Logging;

namespace DLCS.Repository.Messaging
{
    public class MessageBus : IMessageBus
    {
        private readonly ILogger<MessageBus> logger;

        public MessageBus(ILogger<MessageBus> logger)
        {
            this.logger = logger;
        }
        public void SendIngestAssetRequest(IngestAssetRequest ingestAssetRequest)
        {
            logger.LogInformation("Message Bus: " + ingestAssetRequest.ToString());
        } 

        public void SendAssetModifiedNotification(Asset before, Asset after)
        {
            logger.LogInformation("Message Bus: Asset Modified: " + after.Id);
        }
    }
}