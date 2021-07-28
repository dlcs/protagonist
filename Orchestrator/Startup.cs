using Amazon.S3;
using DLCS.Model.Assets;
using DLCS.Model.Customer;
using DLCS.Model.PathElements;
using DLCS.Model.Storage;
using DLCS.Repository;
using DLCS.Repository.Assets;
using DLCS.Repository.Settings;
using DLCS.Repository.Storage.S3;
using DLCS.Web.Configuration;
using DLCS.Web.Requests.AssetDelivery;
using IIIF.ImageApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orchestrator.AV;
using Orchestrator.Images;
using Orchestrator.Settings;
using Serilog;

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
            services.Configure<ThumbsSettings>(configuration.GetSection("Thumbs"));
            services.Configure<ProxySettings>(configuration.GetSection("Proxy"));
            
            services
                .AddLazyCache()
                .AddSingleton<ICustomerRepository, CustomerRepository>()
                .AddSingleton<IPathCustomerRepository, CustomerPathElementRepository>()
                .AddSingleton<IAssetRepository, AssetRepository>()
                .AddSingleton<IAssetDeliveryPathParser, AssetDeliveryPathParser>()
                .AddSingleton<ImageRequestHandler>()
                .AddSingleton<AVRequestHandler>()
                .AddAWSService<IAmazonS3>()
                .AddSingleton<IBucketReader, BucketReader>()
                .AddSingleton<IThumbReorganiser, NonOrganisingReorganiser>()
                .AddSingleton<IThumbRepository, ThumbRepository>();

            services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy",
                    builder => builder
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .SetIsOriginAllowed(host => true)
                        .AllowCredentials());
            });

            services
                .AddHealthChecks()
                .AddNpgSql(configuration.GetPostgresSqlConnection());
            
            // Add the reverse proxy to capability to the server
            var proxyBuilder = services
                .AddReverseProxy()
                .LoadFromConfig(configuration.GetSection("ReverseProxy"));
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILogger<Startup> logger)
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
                    endpoints.MapAVHandling();
                    endpoints.MapHealthChecks("/health");
                });
        }
    }
}