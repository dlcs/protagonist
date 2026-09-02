using DLCS.Core.Caching;
using DLCS.Web;
using DLCS.Web.Configuration;
using DLCS.Web.Logging;
using DLCS.Web.Middleware;
using Engine.Infrastructure;
using Engine.Settings;
using Serilog;

namespace Engine;

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
        services
            .Configure<EngineSettings>(configuration)
            .Configure<CacheSettings>(cachingSection);
        
        services
            .AddAws(configuration, webHostEnvironment)
            .AddHttpContextAccessor()
            .AddQueueMonitoring()
            .AddAssetIngestion(configuration.GetRequired<EngineSettings>(), configuration)
            .AddDataAccess(configuration)
            .AddCaching(cachingSection.GetRequired<CacheSettings>())
            .AddTopicNotifiers()
            .AddCorrelationIdHeaderPropagation()
            .ConfigureHealthChecks();

        services.AddControllers();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILogger<Startup> logger)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseCorrelationId()
            .UseRouting()
            .UseSerilogRequestLogging(opts =>
            {
                opts.GetLevel = LogHelper.ExcludeHealthChecks;
            })
            .UseCors()
            .UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapConfiguredHealthChecks();
            });
    }
}
