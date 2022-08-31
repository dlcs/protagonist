using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Assets;

namespace DLCS.Repository.Assets;

/// <summary>
/// Implementation of <see cref="IBatchRepository"/> using EFCore
/// </summary>
public class BatchRepository : DapperRepository, IBatchRepository
{
    private readonly DlcsContext dlcsContext;

    public BatchRepository(DlcsContext dlcsContext) : base(dlcsContext)
    {
        this.dlcsContext = dlcsContext;
    }

    /// <inheritdoc />
    public async Task<Batch> CreateBatch(int customerId, IReadOnlyList<Asset> assets,
        CancellationToken cancellationToken = default)
    {
        var batch = new Batch
        {
            Completed = 0,
            Count = assets.Count,
            Customer = customerId,
            Errors = 0,
            Submitted = DateTime.UtcNow,
            Superseded = false
        };

        // Note - use Dapper to avoid calling .SaveChanges() and commiting any outstanding changes in dbcontext
        batch.Id = await ExecuteScalarAsync<int>(CreateBatchSql, new { Customer = customerId, Count = assets.Count });

        foreach (var asset in assets)
        {
            asset.Batch = batch.Id;
        }

        return batch;
    }
    
    private const string CreateBatchSql = @"
INSERT INTO ""Batches"" (""Customer"", ""Submitted"", ""Count"", ""Completed"", ""Errors"")
VALUES (@Customer, now() at time zone 'utc', @Count, 0, 0)
RETURNING ""Id"";
";
}