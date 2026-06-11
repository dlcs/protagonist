using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DLCS.AWS.Transcoding.Models;
using DLCS.Model.Assets;

namespace DLCS.Repository.Messaging;

public interface IEngineClient
{
    /// <summary>
    /// Send an ingest request to engine for immediate processing.
    /// This shouldn't be used frequently as it can be relatively long running.
    /// </summary>
    /// <param name="asset">The asset the request is for</param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <returns>HttpStatusCode returned from engine.</returns>
    Task<HttpStatusCode> SynchronousIngest(Asset asset,  CancellationToken cancellationToken = default);

    /// <summary>
    /// Queue an ingest request for engine to asynchronously process.
    /// </summary>
    /// <param name="deliverable">The <see cref="IDeliverable"/> to be ingested</param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <returns>Boolean representing whether request successfully queued</returns>
    Task<bool> AsynchronousIngest(IDeliverable deliverable, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queue a batch of Assets for engine to process
    /// </summary>
    /// <param name="assets">List of assets</param>
    /// <param name="isPriority">Whether request is for priority ingest</param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <returns>Count of items successfully processed</returns>
    Task<int> AsynchronousIngestBatch(IReadOnlyCollection<Asset> assets, bool isPriority,
        CancellationToken cancellationToken);

    /// <summary>
    /// Queue a batch of Adjuncts for engine to process
    /// </summary>
    /// <param name="adjuncts">List of adjuncts to ingest</param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <returns>Count of items successfully processed</returns>
    Task<int> AsynchronousIngestBatch(IReadOnlyCollection<Adjunct> adjuncts, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieve a list of iiif-policy options from engine
    /// </summary>
    /// <param name="cancellationToken">Current cancellation token</param>
    Task<IReadOnlyCollection<string>?> GetAllowedAvPolicyOptions(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves av preset names and details of preset
    /// </summary>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <returns>A dictionary of identifiers and presets</returns>
    Task<IReadOnlyDictionary<string, TranscoderPreset>> GetAvPresets(CancellationToken cancellationToken = default);
}
