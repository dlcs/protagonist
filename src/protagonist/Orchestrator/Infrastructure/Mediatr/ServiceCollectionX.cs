using DLCS.Mediatr.Behaviours;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Orchestrator.Infrastructure.Mediatr;

public static class ServiceCollectionX
{
    /// <summary>
    /// Add MediatR dependencies, RequestHandlers and PipelineBehaviours
    /// </summary>
    /// <param name="services">Current <see cref="IServiceCollection"/> implementation.</param>
    /// <returns>Modified IServiceCollection.</returns>
    public static IServiceCollection AddMediatR(this IServiceCollection services)
        => services
            .AddMediatR(typeof(Startup))
            .AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>))
            .AddScoped(typeof(IPipelineBehavior<,>), typeof(AssetRequestParsingBehavior<,>));
}