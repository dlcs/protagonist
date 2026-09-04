using Amazon.Extensions.NETCore.Setup;
using Amazon.Runtime;
using DLCS.AWS.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DLCS.AWS.Configuration;

/// <summary>
/// <see cref="IAwsClientProvider{T}"/> implementation that provides a client scoped to the customer currently being
/// processed, using credentials from <see cref="ICustomerAwsCredentials"/>.
/// </summary>
/// <remarks>
/// Clients are cached per-customer as creating one involves building the request pipeline and resolving endpoints.
/// The underlying HttpClient is cached and shared process-wide by the AWS SDK, keyed on client configuration only, so
/// a client per customer does not result in a connection pool per customer.
/// </remarks>
public class CustomerScopedAwsClientProvider<T> : IAwsClientProvider<T>, IDisposable
    where T : class, IAmazonService
{
    private readonly ICustomerAwsContext customerContext;
    private readonly ICustomerAwsCredentials customerCredentials;
    private readonly AWSOptions? defaultAwsOptions;
    private readonly ILogger<CustomerScopedAwsClientProvider<T>> logger;
    private readonly CustomerKeyedCache<T> clientCache;

    public CustomerScopedAwsClientProvider(
        ICustomerAwsContext customerContext,
        ICustomerAwsCredentials customerCredentials,
        IOptions<AWSSettings> awsSettings,
        ILogger<CustomerScopedAwsClientProvider<T>> logger,
        AWSOptions? defaultAwsOptions = null)
    {
        this.customerContext = customerContext;
        this.customerCredentials = customerCredentials;
        this.defaultAwsOptions = defaultAwsOptions;
        this.logger = logger;

        var assumeRoleSettings = awsSettings.Value.AssumeRole;
        clientCache = new CustomerKeyedCache<T>(assumeRoleSettings.MaxCachedClients,
            TimeSpan.FromMinutes(assumeRoleSettings.CacheIdleMinutes));
    }

    /// <summary>
    /// Get client for the customer currently being processed.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if no customer has been set for the current operation</exception>
    public T GetClient()
    {
        // Fail closed - falling back to ambient credentials here would give access to every customers assets
        var customer = customerContext.CurrentCustomer ?? throw new InvalidOperationException(
            $"Unable to provide a customer-scoped {typeof(T).Name}, no customer set for current operation. " +
            $"{nameof(ICustomerAwsContext)}.{nameof(ICustomerAwsContext.SetCustomer)}() must be called before making AWS requests");

        return clientCache.GetOrCreate(customer, CreateClient);
    }

    private T CreateClient(int customer)
    {
        logger.LogDebug("Creating customer-scoped {ClientType} for customer {Customer}", typeof(T).Name, customer);

        // Note: client configuration is left at defaults so that all customers share the SDK's cached HttpClient
        var customerOptions = new AWSOptions
        {
            Credentials = customerCredentials.GetCredentials(customer),
            Region = defaultAwsOptions?.Region
        };

        return customerOptions.CreateServiceClient<T>();
    }

    public void Dispose() => clientCache.Dispose();
}
