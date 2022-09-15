using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Processing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DLCS.Repository.Processing;

public class CustomerQueueRepository : IDapperContextRepository, ICustomerQueueRepository
{
    private readonly ILogger<CustomerQueueRepository> logger;
    public DlcsContext DlcsContext { get; }
    
    public CustomerQueueRepository(DlcsContext dlcsContext, ILogger<CustomerQueueRepository> logger)
    {
        this.logger = logger;
        DlcsContext = dlcsContext;
    }

    public async Task<CustomerQueue?> Get(int customer, string name, CancellationToken cancellationToken)
    {
        try
        {
            // NOTE - this is currently difficult to migrate to EF due to how SET operations are handled
            const string sql = @"SELECT 
q.""Customer"", q.""Size"", q.""Name"", b.""BatchesWaiting"", b.""ImagesWaiting"" FROM ""Queues"" q,
	(SELECT COUNT(""Id"") AS ""BatchesWaiting"", SUM(""Count"") - SUM(""Completed"") AS ""ImagesWaiting"" 
    FROM ""Batches"" WHERE ""Customer"" = @customer AND ""Finished"" IS NULL AND ""Superseded"" = false) b
  WHERE q.""Customer"" = @customer AND ""Name"" = @name
";
            return await this.QueryFirstOrDefaultAsync<CustomerQueue>(sql, new {customer, name});
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting customer counts for customer {Customer}, queue {Queue}", customer, name);
            return null;
        }
    }

    public async Task IncrementSize(int customer, string name, int incrementAmount = 1,
        CancellationToken cancellationToken = default)
    {
        await ChangeQueueSize(customer, name, incrementAmount, cancellationToken);
    }

    public async Task DecrementSize(int customer, string name, int decrementAmount = 1,
        CancellationToken cancellationToken = default)
    {
        await ChangeQueueSize(customer, name, -decrementAmount, cancellationToken);
    }

    private async Task ChangeQueueSize(int customer, string name, int amount, CancellationToken cancellationToken)
    {
        await using var transaction =
            await DlcsContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        try
        {
            var queue = await DlcsContext.Queues.SingleOrDefaultAsync(q => q.Customer == customer && q.Name == name,
                cancellationToken: cancellationToken);

            if (queue == null)
            {
                await DlcsContext.Queues.AddAsync(
                    new Queue { Customer = customer, Name = name, Size = Math.Max(0, amount) }, cancellationToken);
            }
            else
            {
                queue.Size += amount;
                if (queue.Size < 0) queue.Size = 0;
            }

            await DlcsContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error incrementing customer {Customer}, queue {QueueName}", customer, name);
            await transaction.RollbackAsync(cancellationToken);
        }
    }
}