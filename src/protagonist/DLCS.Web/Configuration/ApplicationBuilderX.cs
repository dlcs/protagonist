using System.Collections.Generic;
using DLCS.Web.Response;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;

namespace DLCS.Web.Configuration;

/// <summary>
/// Extension methods for <see cref="IApplicationBuilder"/>
/// </summary>
public static class ApplicationBuilderX
{
    /// <summary>
    /// Configure app to use pathBase, if specified. If pathBase null or whitespace then no-op.
    /// </summary>
    /// <param name="app">Current <see cref="IApplicationBuilder"/> instance</param>
    /// <param name="pathBase">PathBase value.</param>
    /// <param name="logger">Current <see cref="ILogger"/> instance</param>
    /// <returns>Current <see cref="IApplicationBuilder"/> instance</returns>
    public static IApplicationBuilder HandlePathBase(this IApplicationBuilder app, string? pathBase, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(pathBase))
        {
            logger.LogDebug("No PathBase specified");
            return app;
        }

        logger.LogInformation("Using PathBase '{PathBase}'", pathBase);
        app.UsePathBase($"/{pathBase}");
        return app;
    }

    /// <summary>
    /// Add swagger + swagger UI to app, respecting pathBase if specified
    /// </summary>
    /// <param name="app">Current <see cref="IApplicationBuilder"/> instance</param>
    /// <param name="name">The name that appears in document selector drop down</param>
    /// <param name="pathBase">Optional pathBase where app is hosted.</param>
    /// <param name="version">The name that appears in document selector drop down</param>
    /// <returns>Current <see cref="IApplicationBuilder"/> instance</returns>
    public static IApplicationBuilder UseSwaggerWithUI(this IApplicationBuilder app, string name,
        string? pathBase = null, string version = "v1")
    {
        var havePathBase = !string.IsNullOrEmpty(pathBase);

        return app
            .UseSwagger(c =>
            {
                if (havePathBase)
                {
                    c.PreSerializeFilters.Add((swaggerDoc, httpReq) =>
                    {
                        swaggerDoc.Servers = new List<OpenApiServer>
                        {
                            new() {Url = $"https://{httpReq.Host.Value}/{pathBase}"}
                        };
                    });
                }
            })
            .UseSwaggerUI(c =>
                c.SwaggerEndpoint(
                    $"/{(havePathBase ? $"{pathBase}/" : string.Empty)}swagger/{version}/swagger.json",
                    name)
            );
    }
    
    /// <summary>
    /// Propagate x-correlation-id header to any downstream calls.
    /// NOTE: This will be added to ALL httpClient requests.
    /// </summary>
    public static IServiceCollection AddCorrelationIdHeaderPropagation(this IServiceCollection services)
    {
        services.AddSingleton<IHttpMessageHandlerBuilderFilter, HeaderPropagationMessageHandlerBuilderFilter>();
        return services;
    }
}
