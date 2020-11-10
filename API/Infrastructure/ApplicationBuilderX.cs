using System.Collections.Generic;
using API.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.OpenApi.Models;

namespace API.Infrastructure
{
    public static class ApplicationBuilderX
    {
        /// <summary>
        /// Add swagger + swagger UI to app
        /// </summary>
        public static IApplicationBuilder UseSwaggerWithUI(this IApplicationBuilder app, IConfiguration configuration)
        {
            var applicationOptions = configuration.Get<ApiSettings>();
            var pathBase = applicationOptions.PathBase;
            var havePathBase = !string.IsNullOrEmpty(pathBase);
            if (havePathBase)
            {
                app.UsePathBase($"/{pathBase}");
            }

            return app
                .UseSwagger(c =>
                {
                    if (havePathBase)
                    {
                        c.PreSerializeFilters.Add((swaggerDoc, httpReq) =>
                        {
                            swaggerDoc.Servers = new List<OpenApiServer>
                                {new OpenApiServer {Url = $"https://{httpReq.Host.Value}/{pathBase}"}};
                        });
                    }
                })
                .UseSwaggerUI(c =>
                    c.SwaggerEndpoint($"/{(havePathBase ? pathBase + "/" : string.Empty)}swagger/v2/swagger.json",
                        "DLCS API"));
        }
    }
}