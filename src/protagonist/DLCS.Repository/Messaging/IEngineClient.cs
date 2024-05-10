using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Assets;
using DLCS.Model.Messaging;

namespace DLCS.Repository.Messaging;

public interface IEngineClient
{
    /// <summary>
    /// Send an ingest request to engine for immediate processing.
    /// This shouldn't be used frequently as it can be relatively long running.
    /// </summary>
    /// <param name="asset">The asset the request is for</param>
    /// <param name="derivativesOnly">If true, only derivatives will be generated</param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <returns>HttpStatusCode returned from engine.</returns>
    Task<HttpStatusCode> SynchronousIngest(Asset asset,  CancellationToken cancellationToken = default);

    /// <summary>
    /// Queue an ingest request for engine to asynchronously process.
    /// </summary>
    /// <param name="asset">The asset the request is for</param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <returns>Boolean representing whether request successfully queued</returns>
    Task<bool> AsynchronousIngest(Asset asset, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queue a batch of ingest requests for engine to process
    /// </summary>
    /// <param name="assets">List of assets</param>
    /// <param name="isPriority">Whether request is for priority ingest</param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <returns>Count of items successfully processed</returns>
    Task<int> AsynchronousIngestBatch(IReadOnlyCollection<Asset> assets,
        bool isPriority, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieve a list of iiif-policy options from engine
    /// </summary>
    /// <param name="cancellationToken">Current cancellation token</param>
    Task<IReadOnlyCollection<string>?> GetAllowedAvPolicyOptions(CancellationToken cancellationToken = default);
}