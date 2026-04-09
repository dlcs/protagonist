using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DLCS.Model.Assets;

public interface IAdjunctBatchRepository
{
    /// <summary>
    /// Creates an AdjunctBatch record for a set of already-upserted adjuncts and sets
    /// Adjunct.Batch FK on each adjunct. Does not create AdjunctBatchAdjunct junction
    /// records or send engine notifications — the caller is responsible for both.
    /// </summary>
    Task<AdjunctBatch> CreateBatch(int customerId, IReadOnlyList<Adjunct> adjuncts,
        CancellationToken cancellationToken);
}
