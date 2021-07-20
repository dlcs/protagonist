using System;
using System.Net;
using System.Net.Http;
using DLCS.Repository;
using DLCS.Web.Configuration;
using DLCS.Web.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Orchestrator.Images;
using Orchestrator.Settings;
using Serilog;
using Yarp.ReverseProxy.Forwarder;

namespace Orchestrator
{
    public class Startup
    {
        private readonly IConfiguration configuration;
        public Startup(IConfiguration configuration)
        {
            this.configuration = configuration;
        }
        
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<OrchestratorSettings>(configuration);

            services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy",
                    builder => builder
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .SetIsOriginAllowed(host => true)
                        .AllowCredentials());
            });
            
            services.AddDbContext<DlcsContext>(opts =>
                opts.UseNpgsql(configuration.GetConnectionString("PostgreSQLConnection"))
            );
            
            services
                .AddHealthChecks()
                .AddDbContextCheck<DlcsContext>("DLCS-DB");
            
            // Add the reverse proxy to capability to the server
            var proxyBuilder = services.AddReverseProxy();
            // Initialize the reverse proxy from the "ReverseProxy" section of configuration
            proxyBuilder.LoadFromConfig(configuration.GetSection("ReverseProxy"));
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILogger<Startup> logger,
            IHttpForwarder forwarder)
        {
            var applicationOptions = configuration.Get<OrchestratorSettings>();
            var pathBase = applicationOptions.PathBase;

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app
                .HandlePathBase(pathBase, logger)
                .UseHttpsRedirection()
                .UseRouting()
                .UseSerilogRequestLogging()
                .UseCors("CorsPolicy")
                .UseAuthorization()
                .UseEndpoints(endpoints =>
                {
                    endpoints.MapReverseProxy();
                    endpoints.MapImageHandling();
                    endpoints.MapHealthChecks("/health");
                });
        }
    }
}