using System.Threading.Tasks;
using DLCS.Model.Processing;
using Microsoft.Extensions.Configuration;

namespace DLCS.Repository.Entities;

public class CustomerQueueRepository : IDapperContextRepository, ICustomerQueueRepository
{
    public DlcsContext DlcsContext { get; }
    
    public CustomerQueueRepository(DlcsContext dlcsContext)
    {
        DlcsContext = dlcsContext;
    }

    public async Task<int> GetSize(int customer, string name)
    {
        return (await DlcsContext.Queues.FindAsync(customer)).Size;
    }

    public async Task<CustomerQueue> Get(int customer, string name)
    {
        const string sql = @"SELECT 
q.""Customer"", q.""Size"", q.""Name"", b.""BatchesWaiting"", b.""ImagesWaiting"" FROM ""Queues"" q,
	(SELECT COUNT(""Id"") AS ""BatchesWaiting"", SUM(""Count"") - SUM(""Completed"") AS ""ImagesWaiting"" 
    FROM ""Batches"" WHERE ""Customer"" = @customer AND ""Finished"" IS NULL AND ""Superseded"" = false) b
  WHERE q.""Customer"" = @customer AND ""Name"" = @name
";
        return await this.QueryFirstOrDefaultAsync<CustomerQueue>(sql, new {customer, name});
    }

    public async Task Put(CustomerQueue queue)
    {
        // This upsert logic is not quite the same as Deliverator
        // /DLCS.PostgreSQL/Data/Store/PostgreSQLCustomerQueueStore.cs#L24
        var upsertedQueue = await DlcsContext.Queues.FindAsync(queue.Customer);
        if (upsertedQueue == null)
        {
            upsertedQueue = new Queue {Customer = queue.Customer};
            await DlcsContext.Queues.AddAsync(upsertedQueue);
        }
        upsertedQueue.Name = queue.Name;
        upsertedQueue.Size = queue.Size;
        await DlcsContext.SaveChangesAsync();
    }

    public async Task Remove(int customer, string name)
    {
        const string sql = @"DELETE FROM ""Queues"" WHERE ""Customer"" = @customer AND ""Name"" = @name";
        await this.ExecuteSqlAsync(sql, new {customer, name});
    }

    public async Task IncrementSize(int customer, string name)
    {
        const string sql = @"UPDATE ""Queues"" SET ""Size"" = ""Size"" + 1 WHERE ""Customer"" = @customer AND ""Name"" = @name";
        await this.ExecuteSqlAsync(sql, new {customer, name});
    }

    public async Task DecrementSize(int customer, string name)
    {
        const string sql = @"UPDATE ""Queues"" SET ""Size"" = ""Size"" - 1 WHERE ""Customer"" = @customer AND ""Name"" = @name";
        await this.ExecuteSqlAsync(sql, new {customer, name});
    }
}