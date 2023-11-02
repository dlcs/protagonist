using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Assets;
using DLCS.Model.PathElements;

namespace DLCS.Model.Messaging;

/// <summary>
/// Interface for transmitting notifications related to <see cref="Asset"/> 
/// </summary>
public interface IIngestNotificationSender
{
    /// <summary>
    /// Enqueue a message that an asset needs to be ingested.
    /// </summary>
    Task<bool> SendIngestAssetRequest(Asset assetToIngest, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enqueue a message for every asset that is to be ingested
    /// </summary>
    /// <param name="assets">List of assets to ingest</param>
    /// <param name="isPriority">If true then assets are added to ingest queue</param>
    Task<int> SendIngestAssetsRequest(IReadOnlyList<Asset> assets, bool isPriority,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send an asset for immediate processing; the call blocks until complete.
    /// </summary>
    /// <param name="derivativesOnly">If true, only derivatives (e.g. thumbs) will be created</param>
    Task<HttpStatusCode> SendImmediateIngestAssetRequest(Asset assetToIngest, bool derivativesOnly,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcast a change to the status of an Asset, for any subscribers.
    /// </summary>
    Task SendAssetModifiedNotification(ChangeType changeType, Asset? before, Asset? after,
        CustomerPathElement? customerPathElement = null);
}