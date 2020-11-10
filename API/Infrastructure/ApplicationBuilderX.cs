using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.OpenApi.Models;

namespace API.Infrastructure
{
    public static class ApplicationBuilderX
    {
        /// <summary>
        /// Add swagger + swagger UI to app
        /// </summary>
        public static IApplicationBuilder UseSwaggerWithUI(this IApplicationBuilder app, string pathBase)
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