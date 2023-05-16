using DLCS.Model.Customers;
namespace Engine.Ingest;

/// <summary>
/// Interface for operations related to ingesting assets.
/// Different workers will handle different types of asset
/// </summary>
public interface IAssetIngesterWorker
{
    /// <summary>
    /// Ingest provided asset using given CustomerOriginStrategy
    /// </summary>
    Task<IngestResultStatus> Ingest(IngestionContext ingestionContext,
        CustomerOriginStrategy customerOriginStrategy,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for operations related to ingesting assets.
/// This is an interface to allow post-processing work to be done, after asset has been saved to database.
/// </summary>
public interface IAssetIngesterPostProcess
{
    /// <summary>
    /// Carry out post-ingest operations
    /// </summary>
    Task PostIngest(IngestionContext ingestionContext, bool ingestSuccessful);
}