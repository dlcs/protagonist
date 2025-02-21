using System;
using DLCS.Core.Types;

namespace DLCS.Model.Assets;

/// <summary>
/// A record of all images that were part of a batch
/// </summary>
public class BatchAsset
{
    public int BatchId { get; set; }
    public AssetId AssetId { get; set; } = null!;
    public BatchAssetStatus Status { get; set; } = BatchAssetStatus.Waiting;
    public string? Error { get; set; }
    public DateTime? Finished { get; set; }
    
    public Batch Batch { get; set; } = null!;
    public Asset Asset { get; set; } = null!;
}

public enum BatchAssetStatus
{
    /// <summary>
    /// Placeholder
    /// </summary>
    Unknown = 0,
    
    /// <summary>
    /// Asset is waiting to be picked up or is in-flight
    /// </summary>
    Waiting = 1,
    
    /// <summary>
    /// Asset failed to ingest
    /// </summary>
    Error = 2,
    
    /// <summary>
    /// Asset completed successfully
    /// </summary>
    Completed = 3,
}