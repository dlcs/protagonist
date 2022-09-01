using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Messaging;

namespace DLCS.Repository.Messaging;

public interface IEngineClient
{
    /// <summary>
    /// Send an ingest request to engine for immediate processing.
    /// This shouldn't be used frequently as it can be relatively long running.
    /// </summary>
    /// <param name="ingestAssetRequest">Request containing details of asset to ingest</param>
    /// <param name="derivativesOnly">If true, only derivatives will be generated</param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <returns>HttpStatusCode returned from engine.</returns>
    Task<HttpStatusCode> SynchronousIngest(IngestAssetRequest ingestAssetRequest, bool derivativesOnly = false,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Queue an ingest request for engine to asynchronously process.
    /// </summary>
    /// <param name="ingestAssetRequest">Request containing details of asset to ingest</param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <returns>Boolean representing whether request successfully queued</returns>
    Task<bool> AsynchronousIngest(IngestAssetRequest ingestAssetRequest, CancellationToken cancellationToken = default);
}