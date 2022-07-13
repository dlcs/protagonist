using DLCS.Model.Customers;
using DLCS.Model.Messaging;

namespace Engine.Ingest.Workers;

/// <summary>
/// Interface for operations related to ingesting assets.
/// Different workers will handle different types of asset
/// </summary>
public interface IAssetIngesterWorker
{
    /// <summary>
    /// Ingest provided asset using given CustomerOriginStrategy
    /// </summary>
    Task<IngestResult> Ingest(IngestAssetRequest ingestAssetRequest,
        CustomerOriginStrategy customerOriginStrategy,
        CancellationToken cancellationToken = default);
}