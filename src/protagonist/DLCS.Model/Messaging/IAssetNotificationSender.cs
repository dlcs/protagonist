using System.Net;
using System.Threading.Tasks;
using DLCS.Model.Assets;

namespace DLCS.Model.Messaging;

public interface IAssetNotificationSender
{
    /// <summary>
    /// Enqueue a message for Engine that an asset needs to be ingested.
    /// </summary>
    /// <param name="ingestAssetRequest"></param>
    Task SendIngestAssetRequest(IngestAssetRequest ingestAssetRequest);
    
    /// <summary>
    /// Send an asset to Engine for immediate processing; the call blocks until Engine responds.
    /// </summary>
    /// <param name="ingestAssetRequest"></param>
    /// <param name="derivativesOnly">Just make new thumbs... or AV transcodes?</param>
    Task<HttpStatusCode> SendImmediateIngestAssetRequest(IngestAssetRequest ingestAssetRequest, bool derivativesOnly);
    
    /// <summary>
    /// Broadcast a change to the status of an Asset, for any subscribers.
    /// </summary>
    Task SendAssetModifiedNotification(ChangeType changeType, Asset? before, Asset? after);
}