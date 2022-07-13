using DLCS.Model.Messaging;
using Engine.Ingest.Models;

namespace Engine.Ingest;

public interface IAssetIngester
{
    /// <summary>
    /// Run ingest based on <see cref="IncomingIngestEvent"/>.
    /// </summary>
    /// <returns>Result of ingest operations</returns>
    /// <remarks>This is to comply with message format sent by Deliverator API.</remarks>
    Task<IngestResult> Ingest(IncomingIngestEvent request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Run ingest based on <see cref="IngestAssetRequest"/>.
    /// </summary>
    /// <returns>Result of ingest operations</returns>
    Task<IngestResult> Ingest(IngestAssetRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Contains operations for ingesting assets.
/// </summary>
public class AssetIngester : IAssetIngester
{
    /// <summary>
    /// Run ingest based on <see cref="IncomingIngestEvent"/>.
    /// </summary>
    /// <returns>Result of ingest operations</returns>
    /// <remarks>This is to comply with message format sent by Deliverator API.</remarks>
    public Task<IngestResult> Ingest(IncomingIngestEvent request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Run ingest based on <see cref="IngestAssetRequest"/>.
    /// </summary>
    /// <returns>Result of ingest operations</returns>
    public async Task<IngestResult> Ingest(IngestAssetRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}