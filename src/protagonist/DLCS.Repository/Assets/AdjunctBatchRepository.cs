using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Assets;

namespace DLCS.Repository.Assets;

/// <summary>
/// Implementation of <see cref="IAdjunctBatchRepository"/> using EFCore
/// </summary>
public class AdjunctBatchRepository(DlcsContext dlcsContext) : IAdjunctBatchRepository
{
    /// <inheritdoc />
    public async Task<AdjunctBatch> CreateBatch(int customerId, IReadOnlyList<Adjunct> adjuncts,
        CancellationToken cancellationToken)
    {
        var externalCount = adjuncts.Count(a => !a.IsHosted());
        var allExternal = externalCount == adjuncts.Count;

        var utcNow = DateTime.UtcNow;
        var batch = new AdjunctBatch
        {
            Customer = customerId,
            Submitted = utcNow,
            Count = adjuncts.Count,
            Completed = externalCount,
            Errors = 0,
            Finished = allExternal ? utcNow : null
        };

        dlcsContext.AdjunctBatches.Add(batch);
        await dlcsContext.SaveChangesAsync(cancellationToken);

        // Set the Batch FK on each adjunct (they are tracked by the context)
        foreach (var adjunct in adjuncts)
        {
            adjunct.Batch = batch.Id;
        }

        return batch;
    }
}
