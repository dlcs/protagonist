using System.Threading;
using System.Threading.Tasks;

namespace DLCS.Model.Processing;

public interface ICustomerQueueRepository
{
    /// <summary>
    /// Get <see cref="CustomerQueue"/> object for specified customer with specified name.
    /// This consists of values from both Queue and Batch tables
    /// </summary>
    Task<CustomerQueue?> Get(int customer, string name, CancellationToken cancellationToken);

    /// <summary>
    /// Increment specified queue by specified amount.
    /// If queue doesn't exist it will be created with Size = incrementAmount
    /// </summary>
    /// <remarks>
    /// The need to potentially create queue is a result of how queues were handled in Deliverator, they are not
    /// guaranteed to exist
    /// </remarks>
    Task IncrementSize(int customer, string name, int incrementAmount = 1,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Decrement specified queue by specified amount.
    /// If queue doesn't exist it will be created with Size = 0
    /// </summary>
    /// <remarks>
    /// The need to potentially create queue is a result of how queues were handled in Deliverator, they are not
    /// guaranteed to exist
    /// </remarks>
    Task DecrementSize(int customer, string name, int decrementAmount = 1,
        CancellationToken cancellationToken = default);
}