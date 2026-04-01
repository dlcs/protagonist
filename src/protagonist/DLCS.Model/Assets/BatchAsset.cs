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
    public BatchStatus Status { get; set; } = BatchStatus.Waiting;
    public string? Error { get; set; }
    public DateTime? Finished { get; set; }
    
    public Batch Batch { get; set; } = null!;
    public Asset Asset { get; set; } = null!;
}
