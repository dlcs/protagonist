using DLCS.Core.Guard;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Orchestrator.Infrastructure.ReverseProxy
{
    public static class EndpointRouteBuilderX
    {
        /// <summary>
        /// Get service from <see cref="IEndpointRouteBuilder"/> service provider
        /// </summary>
        /// <param name="endpoints"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T GetRequiredService<T>(this IEndpointRouteBuilder endpoints)
            => endpoints.ServiceProvider.GetService<T>().ThrowIfNull(typeof(T).Name)!;
    }
}