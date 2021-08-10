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
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orchestrator.Assets;
using Orchestrator.Images;
using Orchestrator.ReverseProxy;
using Orchestrator.Settings;
using Orchestrator.TimeBased;
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
            var reverseProxySection = configuration.GetSection("ReverseProxy");
            services
                .Configure<OrchestratorSettings>(configuration)
                .Configure<ThumbsSettings>(configuration.GetSection("Thumbs"))
                .Configure<ProxySettings>(configuration.GetSection("Proxy"))
                .Configure<ReverseProxySettings>(reverseProxySection);
            
            services
                .AddLazyCache()
                .AddSingleton<ICustomerRepository, CustomerRepository>()
                .AddSingleton<IPathCustomerRepository, CustomerPathElementRepository>()
                .AddSingleton<IAssetRepository, AssetRepository>()
                .AddSingleton<IAssetDeliveryPathParser, AssetDeliveryPathParser>()
                .AddSingleton<ImageRequestHandler>()
                .AddSingleton<TimeBasedRequestHandler>()
                .AddAWSService<IAmazonS3>()
                .AddSingleton<IBucketReader, BucketReader>()
                .AddSingleton<IThumbReorganiser, NonOrganisingReorganiser>()
                .AddSingleton<IThumbRepository, ThumbRepository>()
                .AddSingleton<IAssetTracker, MemoryAssetTracker>();

            var reverseProxySettings = reverseProxySection.Get<ReverseProxySettings>();
            services.AddHttpClient<IDeliveratorClient, DeliveratorClient>(client =>
            {
                client.DefaultRequestHeaders.Add("x-requested-by", "DLCS Protagonist Yarp"); // TODO - add this to all outgoing?
                client.BaseAddress = reverseProxySettings.GetAddressForProxyTarget(ProxyDestination.Orchestrator);
            });

            services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy",
                    builder => builder
                        .AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader());
            });

            services
                .AddHealthChecks()
                .AddNpgSql(configuration.GetPostgresSqlConnection());
            
            // Add the reverse proxy to capability to the server
            services
                .AddReverseProxy()
                .LoadFromConfig(reverseProxySection);
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
                    endpoints.MapTimeBasedHandling();
                    endpoints.MapHealthChecks("/health");
                });
        }
    }
}