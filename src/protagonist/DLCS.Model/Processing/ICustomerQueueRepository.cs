using System.Threading;
using System.Threading.Tasks;

namespace DLCS.Model.Processing;

public interface ICustomerQueueRepository
{
    Task<int> GetSize(int customer, string name);
    
    /// <summary>
    /// Get <see cref="CustomerQueue"/> object for specified customer with specified name
    /// </summary>
    Task<CustomerQueue?> Get(int customer, string name, CancellationToken cancellationToken);
    
    Task Put(CustomerQueue queue);
    
    Task Remove(int customer, string name);
    
    Task IncrementSize(int customer, string name);
    
    Task DecrementSize(int customer, string name);
}