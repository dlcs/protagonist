using System.Threading;
using System.Threading.Tasks;

namespace DLCS.Model.Processing;

public interface ICustomerQueueRepository
{
    /// <summary>
    /// Get <see cref="CustomerQueue"/> object for specified customer with specified name
    /// </summary>
    Task<CustomerQueue?> Get(int customer, string name, CancellationToken cancellationToken);

    Task IncrementSize(int customer, string name, int incrementAmount = 1);
    
    Task DecrementSize(int customer, string name, int incrementAmount = 1);
}