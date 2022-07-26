using System;
using System.Text.Json.Serialization;
using DLCS.Model.Assets;

namespace DLCS.Model.Messaging;

// NOTE: This currently duplicates https://github.com/dlcs/protagonist/blob/engine-rework/Engine/Ingest/Models/IngestAssetRequest.cs
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
    public Asset Asset { get; }

    [JsonConstructor]
    public IngestAssetRequest(Asset asset, DateTime? created)
    {
        Asset = asset;
        Created = created;
    }

    public override string ToString()
    {
        return $"IngestAssetRequest at {Created} for Asset {Asset.Id}";
    }
}