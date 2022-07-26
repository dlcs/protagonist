using System.Threading.Tasks;

namespace DLCS.Model
{
    /// <summary>
    /// This is identical to IEntityCounterStore in Deliverator
    /// </summary>
    public interface IEntityCounterRepository
    {
        Task Create(int customer, string entityType, string scope, long initialValue = 1);
        Task<bool> Exists(int customer, string entityType, string scope);
        Task<long> Get(int customer, string entityType, string scope, long initialValue = 1);
        Task<long> GetNext(int customer, string entityType, string scope, long initialValue = 1);
        Task Reset(int customer, string entityType, string scope);
        Task Set(int customer, string entityType, string scope, long value);
        Task Remove(int customer, string entityType, string scope);

        Task<long> Increment(int customer, string entityType, string scope, long initialValue = 1);
        Task<long> Decrement(int customer, string entityType, string scope, long initialValue = 1);
    }
}