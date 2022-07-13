using DLCS.Model.Customers;
using DLCS.Model.Messaging;

namespace Engine.Ingest.Workers;

public class ImageIngesterWorker : IAssetIngesterWorker
{
    /// <summary>
    /// <see cref="IAssetIngesterWorker"/> for ingesting Image assets (Family = I).
    /// </summary>
    public Task<IngestResult> Ingest(IngestAssetRequest ingestAssetRequest, CustomerOriginStrategy customerOriginStrategy,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}