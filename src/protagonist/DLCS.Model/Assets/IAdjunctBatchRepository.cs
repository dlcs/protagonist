using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DLCS.Model.Assets;

public interface IAdjunctBatchRepository
{
    /// <summary>
    /// Creates an <see cref="AdjunctBatch"/> for a set of already-upserted adjuncts and sets Adjunct.Batch FK on each
    /// adjunct.
    /// </summary>
    /// <remarks>
    /// Does not create <see cref="AdjunctBatchAdjunct"/> junction records - the caller is responsible for that.
    /// </remarks>
    Task<AdjunctBatch> CreateBatch(int customerId, IReadOnlyList<Adjunct> adjuncts,
        CancellationToken cancellationToken);
}
