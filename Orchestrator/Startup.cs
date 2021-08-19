using System.Net.Http;
using Amazon.S3;
using DLCS.Model.Assets;
using DLCS.Model.Customer;
using DLCS.Model.PathElements;
using DLCS.Model.Security;
using DLCS.Model.Storage;
using DLCS.Repository;
using DLCS.Repository.Assets;
using DLCS.Repository.Customers;
using DLCS.Repository.Security;
using DLCS.Repository.Settings;
using DLCS.Repository.Storage.S3;
using DLCS.Repository.Strategy;
using DLCS.Web.Configuration;
using DLCS.Web.Requests.AssetDelivery;
using DLCS.Web.Response;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orchestrator.Assets;
using Orchestrator.Features.Images;
using Orchestrator.Features.TimeBased;
using Orchestrator.Infrastructure.Mediatr;
using Orchestrator.Infrastructure.ReverseProxy;
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
            var reverseProxySection = configuration.GetSection("ReverseProxy");
            services
                .Configure<OrchestratorSettings>(configuration)
                .Configure<ThumbsSettings>(configuration.GetSection("Thumbs"))
                .Configure<ProxySettings>(configuration.GetSection("Proxy"))
                .Configure<CacheSettings>(configuration.GetSection("Caching"))
                .Configure<ReverseProxySettings>(reverseProxySection);
            
            // TODO - configure memoryCache
            services
                .AddLazyCache()
                .AddSingleton<ICustomerRepository, CustomerRepository>()
                .AddSingleton<IPathCustomerRepository, CustomerPathElementRepository>()
                .AddSingleton<IAssetRepository, DapperAssetRepository>()
                .AddSingleton<IAssetDeliveryPathParser, AssetDeliveryPathParser>()
                .AddSingleton<ImageRequestHandler>()
                .AddSingleton<TimeBasedRequestHandler>()
                .AddAWSService<IAmazonS3>()
                .AddSingleton<IBucketReader, BucketReader>()
                .AddSingleton<IThumbReorganiser, NonOrganisingReorganiser>()
                .AddSingleton<IThumbRepository, ThumbRepository>()
                .AddSingleton<IAssetTracker, MemoryAssetTracker>()
                .AddSingleton<ICredentialsRepository, DapperCredentialsRepository>()
                .AddSingleton<IAuthServicesRepository, DapperAuthServicesRepository>()
                .AddScoped<ICustomerOriginStrategyRepository, CustomerOriginStrategyRepository>()
                .AddTransient<IAssetPathGenerator, ConfigDrivenAssetPathGenerator>()
                .AddOriginStrategies()
                .AddDbContext<DlcsContext>(opts =>
                    opts.UseNpgsql(configuration.GetConnectionString("PostgreSQLConnection"))
                )
                .AddMediatR()
                .AddHttpContextAccessor();

            var reverseProxySettings = reverseProxySection.Get<ReverseProxySettings>();
            services
                .AddHttpClient<IDeliveratorClient, DeliveratorClient>(client =>
                {
                    // TODO - add this to all outgoing?
                    client.DefaultRequestHeaders.Add("x-requested-by", "DLCS Protagonist Yarp");
                    client.BaseAddress = reverseProxySettings.GetAddressForProxyTarget(ProxyDestination.Orchestrator);
                })
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseCookies = false });
            
            // Use x-forwarded-host and x-forwarded-proto to set httpContext.Request.Host and .Scheme respectively
            services.Configure<ForwardedHeadersOptions>(opts =>
            {
                opts.ForwardedHeaders = ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedProto;
            });

            services
                .AddControllers()
                .SetCompatibilityVersion(CompatibilityVersion.Latest);
            
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
                .UseForwardedHeaders()
                .UseHttpsRedirection()
                .UseRouting()
                .UseSerilogRequestLogging()
                .UseCors("CorsPolicy")
                .UseAuthorization()
                .UseEndpoints(endpoints =>
                {
                    endpoints.MapControllers();
                    endpoints.MapReverseProxy();
                    endpoints.MapImageHandling();
                    endpoints.MapTimeBasedHandling();
                    endpoints.MapHealthChecks("/health");
                });
        }
    }
}