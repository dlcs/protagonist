using Amazon.Runtime;

namespace DLCS.AWS.Configuration;

/// <summary>
/// Implementation of <see cref="IAwsClientProvider{T}"/> that always returns the client registered in DI. This uses
/// the ambient credentials for the current process (ie with no customer scoping).
/// </summary>
public class AmbientAwsClientProvider<T>(T client) : IAwsClientProvider<T> where T : IAmazonService
{
    public T GetClient() => client;
}
