using System.Threading.Tasks;
using DLCS.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DLCS.Repository.Entities;

public class EntityCounterRepository : IDapperContextRepository, IEntityCounterRepository
{
    private readonly ILogger<EntityCounterRepository> logger;
    public DlcsContext DlcsContext { get; }

    public EntityCounterRepository(DlcsContext dlcsContext, ILogger<EntityCounterRepository> logger)
    {
        this.logger = logger;
        DlcsContext = dlcsContext;
    }

    public async Task<bool> TryCreate(int customer, string entityType, string scope, long initialValue = 1)
    {
        var exists = await Exists(customer, entityType, scope);
        if (exists) return false;

        logger.LogInformation("Creating entityCounter {EntityType}:{Scope} for Customer {Customer}", entityType,
            scope, customer);
        await CreateEntity(customer, entityType, scope, initialValue);
        return true;
    }

    public async Task Remove(int customer, string entityType, string scope, long nextValue)
    {
        var ec = new EntityCounter
        {
            Customer = customer,
            Type = entityType,
            Scope = scope,
            Next = nextValue
        };
        
        DlcsContext.EntityCounters.Remove(ec);
        await DlcsContext.SaveChangesAsync();
    }
    
    public async Task<long> GetNext(int customer, string entityType, string scope, long initialValue = 1)
    {
        return await LongUpdate(GetNextSql, customer, entityType, scope, initialValue);
    }

    public async Task<long> Increment(int customer, string entityType, string scope, long initialValue = 0)
    {
        return await LongUpdate(IncrementSql, customer, entityType, scope, initialValue);
    }

    public async Task<long> Decrement(int customer, string entityType, string scope, long initialValue = 1)
    {
        return await LongUpdate(DecrementSql, customer, entityType, scope, initialValue);
    }

    private Task<bool> Exists(int customer, string entityType, string scope) =>
        DlcsContext.EntityCounters.AnyAsync(ec =>
            ec.Customer == customer && ec.Type == entityType && ec.Scope == scope);
    
    private async Task<long> LongUpdate(string sql, int customer, string entityType, string scope, long initialValue = 1)
    {
        await TryCreate(customer, entityType, scope, initialValue);
        return await this.QuerySingleAsync<long>(sql, new { customer, entityType, scope });
    }
    
    private async Task CreateEntity(int customer, string entityType, string scope, long initialValue = 1)
    {
        var ec = new EntityCounter
        {
            Customer = customer,
            Type = entityType,
            Scope = scope,
            Next = initialValue
        };
        await DlcsContext.EntityCounters.AddAsync(ec);
        await DlcsContext.SaveChangesAsync();
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