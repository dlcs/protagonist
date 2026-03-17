using System;
using System.Diagnostics;
using DLCS.Core.Types;

namespace DLCS.Model.Messaging.Adjunct;

/// <summary>
/// Represents a request to ingest an adjunct.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public class IngestAdjunctRequest(string id, AssetId assetId, DateTime? created)
{
    public const string IngestType = "IngestAdjunct";

    /// <summary>
    /// Get date that this request was created.
    /// </summary>
    public DateTime? Created { get; } = created;

    /// <summary>
    /// Get the id of the adjunct that has to be ingested
    /// </summary>
    public string Id { get; } = id;

    /// <summary>
    /// AssetId of the asset owning the adjunct 
    /// </summary>
    public AssetId AssetId { get; } = assetId;

    private string DebuggerDisplay =>
        $"{nameof(IngestAdjunctRequest)} at {Created} for Adjunct {Id}, AssetId {AssetId}";
}
