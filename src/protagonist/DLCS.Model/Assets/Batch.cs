using System;
using System.Collections.Generic;
using System.Diagnostics;
using DLCS.Core.Types;

namespace DLCS.Model.Assets;

[DebuggerDisplay("{DebuggerDisplay,nq}")]
public class Batch
{
    public int Id { get; set; }
    public int Customer { get; set; }
    public DateTime Submitted { get; set; }
    public int Count { get; set; }
    public int Completed { get; set; }
    public int Errors { get; set; }
    public DateTime? Finished { get; set; }
    
    /// <summary>
    /// Indicates whether all of the images in this batch have subsequently been processed, either in a new bath or
    /// individually
    /// </summary>
    public bool Superseded { get; set; }
    
    public List<BatchAsset>? BatchAssets { get; set; }
    
    private string DebuggerDisplay => $"{Id}, Cust:{Customer}, {Count} item(s)";
}

public static class BatchX
{
    /// <summary>
    /// Add a new <see cref="BatchAsset"/> to <see cref="Batch"/>
    /// </summary>
    public static Batch AddBatchAsset(this Batch batch, AssetId assetId, BatchAssetStatus status = BatchAssetStatus.Waiting)
    {
        (batch.BatchAssets ??= new List<BatchAsset>()).Add(new BatchAsset { AssetId = assetId, Status = status });
        return batch;
    }
}