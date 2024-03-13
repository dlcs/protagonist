using System;
using System.Text.Json.Serialization;
using DLCS.Core.Types;
using DLCS.Model.Assets;

namespace DLCS.Model.Messaging;

/// <summary>
/// Represents a request to ingest an asset.
/// </summary>
public class IngestAssetRequest
{
    /// <summary>
    /// Get date that this request was created.
    /// </summary>
    public DateTime? Created { get; }
    
    /// <summary>
    /// Get Asset to be ingested.
    /// </summary>
    public AssetId Id { get; }

    [JsonConstructor]
    public IngestAssetRequest(AssetId id, DateTime? created)
    {
        Id = id;
        Created = created;
    }

    public override string ToString()
    {
        return $"IngestAssetRequest at {Created} for Asset {Id}";
    }
}