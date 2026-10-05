using DLCS.AWS.Settings;
using DLCS.Core.Caching;
using DLCS.Web;
using DLCS.Web.Configuration;
using DLCS.Web.Logging;
using DLCS.Web.Middleware;
using Engine.Infrastructure;
using Engine.Settings;
using Microsoft.Extensions.Options;
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
        LogAwsClientScoping(app, env, logger);

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

    /// <summary>
    /// Log whether AWS clients are scoped to individual customers. This is a security control, so which mode is in
    /// use is recorded on startup.
    /// </summary>
    private static void LogAwsClientScoping(IApplicationBuilder app, IWebHostEnvironment env,
        ILogger<Startup> logger)
    {
        var awsSettings = app.ApplicationServices.GetRequiredService<IOptions<AWSSettings>>().Value;
        var usingLocalStack = env.IsDevelopment() && awsSettings.UseLocalStack;

        if (!awsSettings.AssumeRole.Enabled)
        {
            logger.LogWarning(
                "AWS:AssumeRole is not enabled - AWS clients use ambient credentials for every customer");
        }
        else if (usingLocalStack)
        {
            logger.LogWarning(
                "AWS:AssumeRole is enabled but LocalStack is in use - AWS clients are not scoped by customer");
        }
        else
        {
            logger.LogInformation("AWS clients are scoped by customer, assuming role {RoleArn}",
                awsSettings.AssumeRole.RoleArn);
        }
    }
}
