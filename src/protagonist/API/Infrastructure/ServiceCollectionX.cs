using System;
using System.IO;
using System.Reflection;
using DLCS.Mediatr.Behaviours;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;

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

        /// <summary>
        /// Add SwaggerGen services to service collection.
        /// </summary>
        public static IServiceCollection ConfigureSwagger(this IServiceCollection services)
            => services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v2", new OpenApiInfo
                {
                    Title = "DLCS API", 
                    Version = "v2",
                    Description = "API for interacting with DLCS"
                });

                c.AddSecurityDefinition(
                    "basic", new OpenApiSecurityScheme
                    {
                        Name = "Authorization",
                        Type = SecuritySchemeType.Http,
                        Scheme = "basic",
                        In = ParameterLocation.Header,
                        Description = "Basic Authorization header using the Bearer scheme.",
                    });

                c.AddSecurityRequirement(new OpenApiSecurityRequirement()
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "basic",
                            },
                        },
                        new string[] { }
                    },
                });
                
                // Set the comments path for the Swagger JSON and UI.
                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                c.IncludeXmlComments(xmlPath);
            });
    }
}