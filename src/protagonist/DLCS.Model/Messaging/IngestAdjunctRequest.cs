using System;
using System.Diagnostics;
using DLCS.Model.Assets;

namespace DLCS.Model.Messaging;

/// <summary>
/// Represents a request to ingest an adjunct.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public class IngestAdjunctRequest(Adjunct adjunct, DateTime? created)
{
    public const string IngestType = "IngestAdjunct";
    
    /// <summary>
    /// Get date that this request was created.
    /// </summary>
    public DateTime? Created { get; } = created;

    /// <summary>
    /// Get the id of the adjunct that has to be ingested
    /// </summary>
    public string Id { get; } = adjunct.Id;
    
    private string DebuggerDisplay => $"{nameof(IngestAdjunctRequest)} at {Created} for Adjunct {Id}";
}
