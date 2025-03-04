using System.Linq;
using System.Threading.Tasks;
using DLCS.Core.Caching;
using DLCS.Repository;
using DLCS.Repository.NamedQueries;
using DLCS.Repository.Strategy.DependencyInjection;
using DLCS.Web.Configuration;
using DLCS.Web.Handlers;
using DLCS.Web.Logging;
using DLCS.Web.Middleware;
using DLCS.Web.Requests.AssetDelivery;
using DLCS.Web.Response;
using DLCS.Web.Views;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orchestrator.Features.Files;
using Orchestrator.Features.Images;
using Orchestrator.Features.TimeBased;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Mediatr;
using Orchestrator.Infrastructure.NamedQueries;
using Orchestrator.Infrastructure.ReverseProxy;
using Orchestrator.Settings;
using Serilog;

// Prevent R# flagging View() as not found
[assembly: AspMvcViewLocationFormat(@"~\Features\Auth\Views\{0}.cshtml")]

namespace Orchestrator;

public class Startup
{
    private readonly IConfiguration configuration;
    private readonly IWebHostEnvironment webHostEnvironment;

    public Startup(IConfiguration configuration, IWebHostEnvironment webHostEnvironment)
    {
        this.configuration = configuration;
        this.webHostEnvironment = webHostEnvironment;
    }

    public void ConfigureServices(IServiceCollection services)
    {
        var cachingSection = configuration.GetSection("Caching");
        var proxySection = configuration.GetSection("Proxy");
        services
            .Configure<OrchestratorSettings>(configuration)
            .Configure<ProxySettings>(proxySection)
            .Configure<NamedQueryTemplateSettings>(configuration)
            .Configure<NamedQuerySettings>(configuration.GetSection("NamedQuery"))
            .Configure<AuthSettings>(configuration.GetSection("Auth"))
            .Configure<PathTemplateOptions>(configuration.GetSection("PathRules"))
            .Configure<CacheSettings>(cachingSection);

        var orchestratorSettings = configuration.Get<OrchestratorSettings>();
        
        services
            .AddTransient<TimingHandler>()
            .AddSingleton<IAssetDeliveryPathParser, AssetDeliveryPathParser>()
            .AddSingleton<ImageRequestHandler>()
            .AddSingleton<TimeBasedRequestHandler>()
            .AddSingleton<FileRequestHandler>()
            .AddSingleton<S3ProxyPathGenerator>()
            .AddTransient<IAssetPathGenerator, ConfigDrivenAssetPathGenerator>()
            .AddSingleton<AssetRequestProcessor>()
            .AddSingleton<DownstreamDestinationSelector>()
            .AddCaching(cachingSection.Get<CacheSettings>())
            .AddOriginStrategies()
            .AddDataAccess(configuration)
            .AddMediatR()
            .AddHttpContextAccessor()
            .AddNamedQueries(configuration)
            .AddOrchestration(orchestratorSettings)
            .AddApiClient(orchestratorSettings)
            .ConfigureHealthChecks(proxySection, configuration)
            .AddAws(configuration, webHostEnvironment)
            .AddCorrelationIdHeaderPropagation()
            .AddInfoJsonClient()
            .AddIIIFBuilding()
            .AddIIIFAuth(orchestratorSettings)
            .HandlePathTemplates();
        
        // Use x-forwarded-host and x-forwarded-proto to set httpContext.Request.Host and .Scheme respectively
        services.Configure<ForwardedHeadersOptions>(opts =>
        {
            opts.ForwardedHeaders = ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedProto;
        });

        services
            .AddFeatureFolderViews()
            .AddControllersWithViews();

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
                jsonFormatter?.SupportedMediaTypes.Add(IIIF.ImageApi.ContentTypes.V2);
                jsonFormatter?.SupportedMediaTypes.Add(IIIF.ImageApi.ContentTypes.V3);
            });
        
        DapperMappings.Register();
        
        // Add the reverse proxy to capability to the server
        services
            .AddReverseProxy()
            .LoadFromConfig(configuration.GetSection("ReverseProxy"));
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILogger<Startup> logger)
    {
        var applicationOptions = configuration.Get<OrchestratorSettings>();
        var pathBase = applicationOptions.PathBase;

        if (env.IsDevelopment())
        {
            DlcsContextConfiguration.TryRunMigrations(configuration, logger);
            app.UseDeveloperExceptionPage();
        }

        app
            .HandlePathBase(pathBase, logger)
            .UseForwardedHeaders()
            .UseRouting()
            .UseOptions()
            .UseHeaderPropagation()
            .UseSerilogRequestLogging(opts =>
            {
                opts.GetLevel = LogHelper.ExcludeHealthChecks;
            })
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
                endpoints.MapFileHandling();
                endpoints.MapConfiguredHealthChecks();
            });
    }
}
