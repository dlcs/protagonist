using System;
using System.Net.Http;
using DLCS.Model.Customers;
using DLCS.Repository.Strategy.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace DLCS.Repository.Strategy.DependencyInjection;

/// <summary>
/// Delegate for getting <see cref="IOriginStrategy"/> implementation for specified strategy.
/// </summary>
public delegate IOriginStrategy OriginStrategyResolver(OriginStrategyType originStrategy);

/// <summary>
/// <see cref="IServiceCollection"/> extensions for registering OriginStrategy implementations
/// </summary>
public static class ServiceCollectionX
{
    /// <summary>
    /// Register all <see cref="IOriginStrategy"/> implementations. Resolve <see cref="OriginStrategyResolver"/>
    /// to get specific implementation by <see cref="OriginStrategyType"/>.
    /// </summary>
    /// <param name="services">Current <see cref="IServiceCollection"/> object</param>
    /// <returns>Modified service collection</returns>
    public static IServiceCollection AddOriginStrategies(this IServiceCollection services)
    {
        services
            .AddSingleton<S3AmbientOriginStrategy>()
            .AddSingleton<DefaultOriginStrategy>()
            .AddSingleton<BasicHttpAuthOriginStrategy>()
            .AddSingleton<SftpOriginStrategy>()
            .AddScoped<OriginFetcher>()
            .AddSingleton<IFileSaver, FileSaver>()
            .AddSingleton<OriginStrategyResolver>(provider => strategy => strategy switch
            {
                OriginStrategyType.Default => provider.GetRequiredService<DefaultOriginStrategy>(),
                OriginStrategyType.BasicHttp => provider.GetRequiredService<BasicHttpAuthOriginStrategy>(),
                OriginStrategyType.S3Ambient => provider.GetRequiredService<S3AmbientOriginStrategy>(),
                OriginStrategyType.SFTP => provider.GetRequiredService<SftpOriginStrategy>(),
                _ => throw new ArgumentOutOfRangeException(nameof(strategy), strategy, null)
            });
        
        services
            .AddHttpClient(HttpClients.OriginStrategy, client =>
            {
                client.DefaultRequestHeaders.Add("Accept", "*/*");
                client.DefaultRequestHeaders.Add("User-Agent", "DLCS/2.0");
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 8
            });

        return services;
    }
}