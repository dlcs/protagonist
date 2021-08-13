using System;
using DLCS.Model.Customer;
using Microsoft.Extensions.DependencyInjection;

namespace DLCS.Repository.Strategy
{
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
            => services
                .AddScoped<IOriginStrategy, S3AmbientOriginStrategy>()
                .AddSingleton<IOriginStrategy, DefaultOriginStrategy>()
                .AddSingleton<IOriginStrategy, BasicHttpAuthOriginStrategy>()
                .AddSingleton<IOriginStrategy, SftpOriginStrategy>()
                .AddTransient<OriginStrategyResolver>(provider => strategy => strategy switch
                {
                    OriginStrategyType.Default => provider.GetService<DefaultOriginStrategy>(),
                    OriginStrategyType.BasicHttp => provider.GetService<BasicHttpAuthOriginStrategy>(),
                    OriginStrategyType.S3Ambient => provider.GetService<S3AmbientOriginStrategy>(),
                    OriginStrategyType.SFTP => provider.GetService<SftpOriginStrategy>(),
                    _ => throw new ArgumentOutOfRangeException(nameof(strategy), strategy, null)
                });
    }
}