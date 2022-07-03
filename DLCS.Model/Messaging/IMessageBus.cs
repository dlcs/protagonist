using System.Net;
using System.Threading.Tasks;
using DLCS.Model.Assets;

namespace DLCS.Model.Messaging
{
    public interface IMessageBus
    {
        /// <summary>
        /// Send a Message to Engine that an asset needs to be ingested.
        /// </summary>
        /// <param name="ingestAssetRequest"></param>
        /// <returns></returns>
        Task SendIngestAssetRequest(IngestAssetRequest ingestAssetRequest);
        
        /// <summary>
        /// Send an asset to Engine for immediate processing; the call blocks until Engine responds.
        /// </summary>
        /// <param name="ingestAssetRequest"></param>
        /// <param name="derivativesOnly">Just make new thumbs... or AV transcodes?</param>
        /// <returns></returns>
        Task<HttpStatusCode> SendImmediateIngestAssetRequest(IngestAssetRequest ingestAssetRequest, bool derivativesOnly);
        
        /// <summary>
        /// Broadcast a change to the status of an Asset, for any subscribers.
        /// </summary>
        /// <param name="before"></param>
        /// <param name="after"></param>
        /// <returns></returns>
        Task SendAssetModifiedNotification(Asset before, Asset after);
    }
}