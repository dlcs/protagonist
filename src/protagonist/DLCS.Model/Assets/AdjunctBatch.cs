using System;
using System.Collections.Generic;
using DLCS.Core.Types;

namespace DLCS.Model.Assets;

public class AdjunctBatch : IDeliverableBatch
{
    public int Id { get; set; }
    public int Customer { get; set; }
    public DateTime Submitted { get; set; }
    public int Count { get; set; }
    public int Completed { get; set; }
    public int Errors { get; set; }
    public DateTime? Finished { get; set; }
    public List<AdjunctBatchAdjunct>? BatchAdjuncts { get; set; }
}

public static class AdjunctBatchX
{
    /// <summary>
    /// Add a new <see cref="AdjunctBatchAdjunct"/> to <see cref="AdjunctBatch"/>
    /// </summary>
    public static AdjunctBatch AddAdjunctBatchAdjunct(this AdjunctBatch batch, string adjunctId, AssetId assetId,
        BatchStatus status = BatchStatus.Waiting)
    {
        (batch.BatchAdjuncts ??= []).Add(new AdjunctBatchAdjunct
        {
            AdjunctId = adjunctId, AssetId = assetId, Status = status
        });
        return batch;
    }
}
