using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Assets;

namespace DLCS.Repository.Assets;

/// <summary>
/// Implementation of <see cref="IBatchRepository"/> using EFCore
/// </summary>
public class BatchRepository : IDapperContextRepository, IBatchRepository
{
    public DlcsContext DlcsContext { get; }

    public BatchRepository(DlcsContext dlcsContext)
    {
        DlcsContext = dlcsContext;
    }

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
        DlcsContext.Batches.Add(batch);
        await DlcsContext.SaveChangesAsync(cancellationToken);

        foreach (var asset in assets)
        {
            asset.Batch = batch.Id;
        }

        return batch;
    }
}