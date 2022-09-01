using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Assets;

namespace DLCS.Model.Messaging;

/// <summary>
/// Interface for transmitting notifications related to <see cref="Asset"/> 
/// </summary>
public interface IAssetNotificationSender
{
    /// <summary>
    /// Enqueue a message that an asset needs to be ingested.
    /// </summary>
    Task<bool> SendIngestAssetRequest(Asset assetToIngest, CancellationToken cancellationToken = default);

    /// <summary>
    /// Send an asset for immediate processing; the call blocks until complete.
    /// </summary>
    /// <param name="derivativesOnly">If true, only derivatives (e.g. thumbs) will be created</param>
    Task<HttpStatusCode> SendImmediateIngestAssetRequest(Asset assetToIngest, bool derivativesOnly,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Broadcast a change to the status of an Asset, for any subscribers.
    /// </summary>
    Task SendAssetModifiedNotification(ChangeType changeType, Asset? before, Asset? after);
}