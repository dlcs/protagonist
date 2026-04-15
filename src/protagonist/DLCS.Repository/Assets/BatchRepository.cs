using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Assets;

namespace DLCS.Repository.Assets;

/// <summary>
/// Implementation of <see cref="IBatchRepository"/> using EFCore
/// </summary>
public class BatchRepository(DlcsContext dlcsContext) : IBatchRepository
{
    /// <inheritdoc />
    public async Task<Batch> CreateBatch(int customerId, IReadOnlyList<Asset> assets,
        CancellationToken cancellationToken, Action<Batch>? postCreate = null)
    {
        var batch = new Batch
        {
            Completed = 0,
            Count = assets.Count,
            Customer = customerId,
            Errors = 0,
            Submitted = DateTime.UtcNow,
            Superseded = false,
            BatchAssets = new List<BatchAsset>(assets.Count)
        };
        
        postCreate?.Invoke(batch);
        dlcsContext.Batches.Add(batch);
        await dlcsContext.SaveChangesAsync(cancellationToken);

        foreach (var asset in assets)
        {
            asset.Batch = batch.Id;
        }

        return batch;
    }
}
