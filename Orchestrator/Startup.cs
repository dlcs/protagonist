using System;
using System.Net;
using System.Net.Http;
using DLCS.Web.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
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
            
            // services.AddControllers();
            
            // Add the reverse proxy to capability to the server
            var proxyBuilder = services.AddReverseProxy();
            // Initialize the reverse proxy from the "ReverseProxy" section of configuration
            proxyBuilder.LoadFromConfig(configuration.GetSection("ReverseProxy"));
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILogger<Startup> logger,
            IHttpForwarder forwarder)
        {
            // TODO - move all of this
            var httpClient = new HttpMessageInvoker(new SocketsHttpHandler
            {
                UseProxy = false,
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.GZip,
                UseCookies = false
            });
            
            var transformer = HttpTransformer.Default;
            var requestOptions = new ForwarderRequestConfig { Timeout = TimeSpan.FromSeconds(100) };

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
                    endpoints.Map("/iiif-img/{**catch-all}", async httpContext =>
                    {
                        // TODO - move this to a separate handler 
                        // TODO - get the deliverator id from config, not hardcoded. This is currently going to httpHole
                        var error = await forwarder.SendAsync(httpContext, "http://127.0.0.1:8081", httpClient,
                            requestOptions, transformer);
                        // Check if the proxy operation was successful
                        if (error != ForwarderError.None)
                        {
                            var errorFeature = httpContext.Features.Get<IForwarderErrorFeature>();
                            var exception = errorFeature.Exception;
                        }
                    });

                    // endpoints.MapControllers();
                });
        }
    }
}