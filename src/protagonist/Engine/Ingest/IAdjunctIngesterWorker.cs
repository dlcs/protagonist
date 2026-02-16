using DLCS.Model.Customers;

namespace Engine.Ingest;

public interface IAdjunctIngesterWorker
{
    /// <summary>
    /// Ingest provided adjunct using given CustomerOriginStrategy
    /// </summary>
    Task<IngestResultStatus> Ingest(AdjunctIngestionContext ingestionContext,
        CustomerOriginStrategy customerOriginStrategy,
        CancellationToken cancellationToken = default); 
}
