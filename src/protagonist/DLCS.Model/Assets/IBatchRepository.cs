using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DLCS.Model.Assets;

/// <summary>
/// Repository for handling and updating <see cref="Batch"/> resources
/// </summary>
public interface IBatchRepository
{
    /// <summary>
    /// Create new Batch in DB and assign Batch property on all provided Assets.
    /// Note: Batch property on Asset is _not_ persisted to DB.
    /// </summary>
    /// <param name="customerId">Id of customer to create batch for.</param>
    /// <param name="assets"></param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <returns>Created Batch object</returns>
    Task<Batch> CreateBatch(int customerId, IReadOnlyList<Asset> assets, CancellationToken cancellationToken = default);
}