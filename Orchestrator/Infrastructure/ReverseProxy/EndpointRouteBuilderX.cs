using System;
using System.Collections.Generic;
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

    public static class ProxyStateLookupUtils
    {
        public static IDictionary<string, T> ToDictionaryByUniqueId<T>(this IEnumerable<T> services, Func<T, string> idSelector)
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            var result = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);

            foreach (var service in services)
            {
                if (!result.TryAdd(idSelector(service), service))
                {
                    throw new ArgumentException($"More than one {typeof(T)} found with the same identifier.", nameof(services));
                }
            }

            return result;
        }
        
        public static T GetRequiredServiceById<T>(this IDictionary<string, T> services, string? id, string defaultId)
        {
            var lookup = id;
            if (string.IsNullOrEmpty(lookup))
            {
                lookup = defaultId;
            }

            if (!services.TryGetValue(lookup, out var result))
            {
                throw new ArgumentException($"No {typeof(T)} was found for the id '{lookup}'.", nameof(id));
            }
            return result;
        }
    }
}