namespace DLCS.AWS.Configuration;

/// <summary>
/// Records which customer the current operation is being carried out for, so that AWS clients can be scoped to that
/// customer.
/// </summary>
public interface ICustomerAwsContext
{
    /// <summary>
    /// Customer that the current operation is for, if set.
    /// </summary>
    int? CurrentCustomer { get; }

    /// <summary>
    /// Set the customer for the current operation. The previous value is restored when the returned object is
    /// disposed.
    /// </summary>
    IDisposable SetCustomer(int customer);
}

/// <summary>
/// <see cref="ICustomerAwsContext"/> implementation that stores the current customer in an <see cref="AsyncLocal{T}"/>
/// so that it flows through the async operations carried out for that customer.
/// </summary>
public class AsyncLocalCustomerAwsContext : ICustomerAwsContext
{
    private static readonly AsyncLocal<int?> CurrentCustomerValue = new();

    public int? CurrentCustomer => CurrentCustomerValue.Value;

    public IDisposable SetCustomer(int customer)
    {
        var previous = CurrentCustomerValue.Value;
        CurrentCustomerValue.Value = customer;
        return new CustomerScope(previous);
    }

    private class CustomerScope(int? previous) : IDisposable
    {
        public void Dispose() => CurrentCustomerValue.Value = previous;
    }
}
