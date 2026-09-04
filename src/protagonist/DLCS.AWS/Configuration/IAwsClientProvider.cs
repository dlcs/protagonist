using Amazon.Runtime;

namespace DLCS.AWS.Configuration;

/// <summary>
/// Provides the AWS service client of type {T} to use for the current operation.
/// </summary>
/// <remarks>
/// This is a seam that allows consumers to be unaware of whether they are using a single, shared client or one that
/// is scoped to the customer currently being processed. See
/// <see cref="AWSConfiguration.SetupAWS"/> for how implementations are registered.
/// </remarks>
public interface IAwsClientProvider<out T> where T : IAmazonService
{
    /// <summary>
    /// Get the client to use for the current operation.
    /// </summary>
    T GetClient();
}

/// <summary>
/// Implementation of <see cref="IAwsClientProvider{T}"/> that always returns the client registered in DI. This uses
/// the ambient credentials for the current process, with no customer scoping.
/// </summary>
public class AmbientAwsClientProvider<T>(T client) : IAwsClientProvider<T> where T : IAmazonService
{
    public T GetClient() => client;
}
