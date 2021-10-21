using System.Threading.Tasks;
using DLCS.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace DLCS.Repository.Entities
{
    public class EntityCounterRepository : DapperRepository, IEntityCounterRepository
    {
        private readonly DlcsContext dlcsContext;

        public EntityCounterRepository(
            IConfiguration configuration, 
            DlcsContext dlcsContext) : base(configuration)
        {
            this.dlcsContext = dlcsContext;
        }

        private async Task<EntityCounter> GetEntityCounter(int customer, string entityType, string scope)
        {
            var entityCounter = await dlcsContext.EntityCounters.SingleAsync(ec =>
                ec.Customer == customer && ec.Type == entityType && ec.Scope == scope);
            return entityCounter;
        }
        
        public async Task Create(int customer, string entityType, string scope, long initialValue = 1)
        {
            var ec = new EntityCounter
            {
                Customer = customer,
                Type = entityType,
                Scope = scope,
                Next = initialValue
            };
            await dlcsContext.EntityCounters.AddAsync(ec);
            await dlcsContext.SaveChangesAsync();
        }

        public async Task<bool> Exists(int customer, string entityType, string scope)
        {
            return await dlcsContext.EntityCounters.AnyAsync(ec =>
                ec.Customer == customer && ec.Type == entityType && ec.Scope == scope);
        }

        public async Task<long> Get(int customer, string entityType, string scope, long initialValue = 1)
        {
            await EnsureCounter(customer, entityType, scope, initialValue);
            var entityCounter = await GetEntityCounter(customer, entityType, scope);
            return entityCounter.Next;
        }

        public async Task<long> GetNext(int customer, string entityType, string scope, long initialValue = 1)
        {
            return await LongUpdate(GetNextSql, customer, entityType, scope, initialValue);
        }
        
        public async Task Set(int customer, string entityType, string scope, long value)
        {
            var entityCounter = await GetEntityCounter(customer, entityType, scope);
            entityCounter.Next = value;
            await dlcsContext.SaveChangesAsync();
        }

        public async Task Reset(int customer, string entityType, string scope)
        {
            await Set(customer, entityType, scope, 0);
        }


        public async Task Remove(int customer, string entityType, string scope)
        {
            var entityCounter = await GetEntityCounter(customer, entityType, scope);
            dlcsContext.EntityCounters.Remove(entityCounter);
            await dlcsContext.SaveChangesAsync();
        }

        public async Task<long> Increment(int customer, string entityType, string scope, long initialValue = 1)
        {
            return await LongUpdate(IncrementSql, customer, entityType, scope, initialValue);
        }

        public async Task<long> Decrement(int customer, string entityType, string scope, long initialValue = 1)
        {
            return await LongUpdate(DecrementSql, customer, entityType, scope, initialValue);
        }

        private async Task EnsureCounter(int customer, string entityType, string scope, long initialValue)
        {
            var exists = await Exists(customer, entityType, scope);
            if (!exists)
            {
                await Create(customer, entityType, scope, initialValue);
            }
        }
        
        // Dapper section, for queries that return a modified counter in one operation
        private async Task<long> LongUpdate(string sql, int customer, string entityType, string scope, long initialValue = 1)
        {
            await EnsureCounter(customer, entityType, scope, initialValue);
            return await QuerySingleAsync<long>(sql, new {customer, entityType, scope});
        }


        private const string GetNextSql = @"
UPDATE ""EntityCounters"" SET ""Next"" = ""Next"" + 1
WHERE ""Type""=@entityType AND ""Scope""=@scope AND ""Customer""=@customer
RETURNING ""Next"" - 1";
        
        private const string IncrementSql = @"
UPDATE ""EntityCounters""
    SET ""Next"" = ""Next"" + 1
WHERE ""Type""=@entityType AND ""Scope""=@scope AND ""Customer""=@customer
RETURNING ""Next""
";
        
        private const string DecrementSql = @"
UPDATE ""EntityCounters""
    SET ""Next"" = ""Next"" - 1
WHERE ""Type""=@entityType AND ""Scope""=@scope AND ""Customer""=@customer
RETURNING ""Next""
";        
    }
}