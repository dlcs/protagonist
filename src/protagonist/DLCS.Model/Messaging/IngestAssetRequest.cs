using System;
using System.Diagnostics;
using System.Text.Json.Serialization;
using DLCS.Core.Types;

namespace DLCS.Model.Messaging;

/// <summary>
/// Represents a request to ingest an asset.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public class IngestAssetRequest
{
    /// <summary>
    /// Get date that this request was created.
    /// </summary>
    public DateTime? Created { get; }
    
    /// <summary>
    /// AssetId to be ingested.
    /// </summary>
    public AssetId Id { get; }
    
    /// <summary>
    /// BatchId this ingestion operation is for
    /// </summary>
    /// <remarks>
    /// When we fetch the Asset we can fetch the latest Batch it's associated with but that might be wrong
    /// </remarks>
    public int? BatchId { get; }

    [JsonConstructor]
    public IngestAssetRequest(AssetId id, DateTime? created, int? batchId)
    {
        Id = id;
        Created = created;
        
        // 0 batchId represents no batch. It's cleaner if we catch that here so that engine only sees null
        BatchId = batchId is > 0 ? batchId : null;
    }

    private string DebuggerDisplay => $"IngestAssetRequest at {Created} for Asset {Id}";
}