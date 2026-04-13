using System;
using DLCS.Core.Types;

namespace DLCS.Model.Assets;

/// <summary>
/// A record of all <see cref="Adjunct"/> that were part of an <see cref="AdjunctBatch"/>
/// </summary>
public class AdjunctBatchAdjunct : IDeliverableBatchItem
{
    public int BatchId { get; set; }
    public string AdjunctId { get; set; } = null!;
    public AssetId AssetId { get; set; }
    public AdjunctBatch Batch { get; set; } = null!;
    public BatchStatus Status { get; set; } = BatchStatus.Waiting;
    public string? Error { get; set; }
    public DateTime? Finished { get; set; }
}
