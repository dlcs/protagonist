using System.Linq;
using System.Threading.Tasks;
using Amazon.S3;
using DLCS.Model.Storage;
using DLCS.Repository;
using DLCS.Repository.Assets;
using DLCS.Repository.Auth;
using DLCS.Repository.Caching;
using DLCS.Repository.Settings;
using DLCS.Repository.Storage.S3;
using DLCS.Repository.Strategy.DependencyInjection;
using DLCS.Web.Configuration;
using DLCS.Web.Requests.AssetDelivery;
using DLCS.Web.Response;
using DLCS.Web.Views;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orchestrator.Assets;
using Orchestrator.Features.Auth;
using Orchestrator.Features.Images;
using Orchestrator.Features.Images.Orchestration;
using Orchestrator.Features.Images.Orchestration.Status;
using Orchestrator.Features.TimeBased;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Auth;
using Orchestrator.Infrastructure.IIIF;
using Orchestrator.Infrastructure.Mediatr;
using Orchestrator.Infrastructure.NamedQueries;
using Orchestrator.Settings;
using Serilog;

// Prevent R# flagging View() as not found
[assembly: AspMvcViewLocationFormat(@"~\Features\Auth\Views\{0}.cshtml")]

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
            var cachingSection = configuration.GetSection("Caching");
            services
                .Configure<OrchestratorSettings>(configuration)
                .Configure<ThumbsSettings>(configuration.GetSection("Thumbs"))
                .Configure<ProxySettings>(configuration.GetSection("Proxy"))
                .Configure<NamedQuerySettings>(configuration.GetSection("NamedQuery"))
                .Configure<CacheSettings>(cachingSection)
                .Configure<ReverseProxySettings>(reverseProxySection);

            services
                .AddSingleton<IAssetDeliveryPathParser, AssetDeliveryPathParser>()
                .AddSingleton<ImageRequestHandler>()
                .AddSingleton<TimeBasedRequestHandler>()
                .AddAWSService<IAmazonS3>()
                .AddSingleton<IBucketReader, BucketReader>()
                .AddSingleton<IThumbReorganiser, NonOrganisingReorganiser>()
                .AddSingleton<IAssetTracker, MemoryAssetTracker>()
                .AddSingleton<IImageOrchestrator, ImageOrchestrator>()
                .AddSingleton<IImageOrchestrationStatusProvider, FileBasedStatusProvider>()
                .AddTransient<IAssetPathGenerator, ConfigDrivenAssetPathGenerator>()
                .AddScoped<AccessChecker>()
                .AddScoped<IIIFCanvasFactory>()
                .AddScoped<ISessionAuthService, SessionAuthService>()
                .AddScoped<AuthCookieManager>()
                .AddSingleton<AssetRequestProcessor>()
                .AddScoped<IAssetAccessValidator, AssetAccessValidator>()
                .AddScoped<IRoleProviderService, HttpAwareRoleProviderService>()
                .AddCaching(cachingSection.Get<CacheSettings>())
                .AddOriginStrategies()
                .AddDataAccess(configuration)
                .AddMediatR()
                .AddHttpContextAccessor()
                .AddNamedQueries(configuration)
                .AddApiClient(configuration.Get<OrchestratorSettings>())
                .ConfigureHealthChecks(reverseProxySection, configuration);
            
            // Use x-forwarded-host and x-forwarded-proto to set httpContext.Request.Host and .Scheme respectively
            services.Configure<ForwardedHeadersOptions>(opts =>
            {
                opts.ForwardedHeaders = ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedProto;
            });

            services
                .AddFeatureFolderViews()
                .AddControllersWithViews()
                .SetCompatibilityVersion(CompatibilityVersion.Latest);

            services
                .AddCors(options =>
                {
                    options.AddPolicy("CorsPolicy",
                        builder => builder
                            .AllowAnyOrigin()
                            .AllowAnyMethod()
                            .AllowAnyHeader());
                })
                .AddMvcCore(options =>
                {
                    var jsonFormatter = options.OutputFormatters.OfType<SystemTextJsonOutputFormatter>()
                        .FirstOrDefault();
                    jsonFormatter?.SupportedMediaTypes.Add(IIIF.Presentation.ContentTypes.V2);
                    jsonFormatter?.SupportedMediaTypes.Add(IIIF.Presentation.ContentTypes.V3);
                });
            
            DapperMappings.Register();
            
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
                .UseRouting()
                .UseSerilogRequestLogging()
                .UseCors("CorsPolicy")
                .UseAuthorization()
                .UseEndpoints(endpoints =>
                {
                    endpoints.MapGet("favicon.ico", context =>
                    {
                        context.Response.StatusCode = 404;
                        return Task.CompletedTask;
                    });
                    endpoints.MapControllers();
                    endpoints.MapReverseProxy();
                    endpoints.MapImageHandling();
                    endpoints.MapTimeBasedHandling();
                    endpoints.MapConfiguredHealthChecks();
                });
        }
    }
}