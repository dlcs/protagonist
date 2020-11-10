using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace API.Infrastructure
{
    public static class ServiceCollectionX
    {
        /// <summary>
        /// Add MediatR services and pipeline behaviours to service collection.
        /// </summary>
        public static IServiceCollection ConfigureMediatR(this IServiceCollection services)
            => services
                .AddMediatR(typeof(Startup))
                .AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
    }
}