using System.Threading.Tasks;

namespace DLCS.Model;

/// <summary>
/// Repo for interacting with EntityCounters
/// </summary>
/// <remarks>This is identical to IEntityCounterStore in Deliverator</remarks>
public interface IEntityCounterRepository
{
    /// <summary>
    /// Create a new EntityCounter record with specified value.
    /// </summary>
    Task Create(int customer, string entityType, string scope, long initialValue = 1);
    
    /// <summary>
    /// Removes an EntityCounter from the database
    /// </summary>
    Task Remove(int customer, string entityType, string scope, long nextValue);
    
    /// <summary>
    /// Increment stored EntityCounter, and return 'next' value (stored/new value + 1)
    /// </summary>
    Task<long> GetNext(int customer, string entityType, string scope, long initialValue = 1);
    
    /// <summary>
    /// Increment stored EntityCounter, and return new value
    /// </summary>
    Task<long> Increment(int customer, string entityType, string scope, long initialValue = 0);

    /// <summary>
    /// Decrement stored EntityCounter, and return new value
    /// </summary>
    Task<long> Decrement(int customer, string entityType, string scope, long initialValue = 1);
}