using Amazon.Runtime;
using Amazon.Runtime.Credentials;
using DLCS.AWS.Settings;
using DLCS.Core.Guard;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DLCS.AWS.Configuration;

/// <summary>
/// Provides <see cref="AWSCredentials"/> scoped to a single customer.
/// </summary>
public interface ICustomerAwsCredentials
{
    /// <summary>
    /// Get credentials to use when making requests on behalf of specified customer.
    /// </summary>
    AWSCredentials GetCredentials(int customer);
}

/// <summary>
/// <see cref="ICustomerAwsCredentials"/> implementation that assumes a role, tagging the session with the customer.
/// </summary>
/// <remarks>
/// Credentials are cached per-customer and shared by every client type, so each customer results in a single STS
/// session rather than one per client. <see cref="AssumeRoleAWSCredentials"/> handles fetching and refreshing the
/// session automatically, so cached credentials remain valid indefinitely.
/// </remarks>
public class AssumedRoleCustomerAwsCredentials : ICustomerAwsCredentials, IDisposable
{
    private readonly AssumeRoleSettings assumeRoleSettings;
    private readonly ILogger<AssumedRoleCustomerAwsCredentials> logger;
    private readonly CustomerKeyedCache<AWSCredentials> credentialsCache;

    // Credentials for the current task, used to assume the customer-scoped role
    private readonly Lazy<AWSCredentials> ambientCredentials =
        new(() => DefaultAWSCredentialsIdentityResolver.GetCredentials());

    public AssumedRoleCustomerAwsCredentials(IOptions<AWSSettings> awsSettings,
        ILogger<AssumedRoleCustomerAwsCredentials> logger)
    {
        assumeRoleSettings = awsSettings.Value.AssumeRole;
        this.logger = logger;
        credentialsCache = new CustomerKeyedCache<AWSCredentials>(assumeRoleSettings.MaxCachedClients,
            TimeSpan.FromMinutes(assumeRoleSettings.CacheIdleMinutes));
    }

    public AWSCredentials GetCredentials(int customer) => credentialsCache.GetOrCreate(customer, CreateCredentials);

    private AWSCredentials CreateCredentials(int customer)
    {
        var roleArn = assumeRoleSettings.RoleArn.ThrowIfNullOrWhiteSpace(nameof(assumeRoleSettings.RoleArn));

        logger.LogDebug("Assuming role {RoleArn} for customer {Customer}", roleArn, customer);

        var options = new AssumeRoleAWSCredentialsOptions
        {
            Tags = [new KeyValuePair<string, string>(assumeRoleSettings.TagKey, customer.ToString())],

            // Ensure the customer tag survives any further role assumption
            TransitiveTagKeys = [assumeRoleSettings.TagKey],
            DurationSeconds = assumeRoleSettings.DurationSeconds
        };

        return new AssumeRoleAWSCredentials(ambientCredentials.Value, roleArn,
            $"{assumeRoleSettings.SessionNamePrefix}{customer}", options);
    }

    public void Dispose() => credentialsCache.Dispose();
}
