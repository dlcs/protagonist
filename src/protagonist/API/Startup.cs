using System.Security.Claims;
using API.Auth;
using API.Features.Customer;
using API.Features.Customer.Requests;
using API.Features.DeliveryChannels.Validation;
using API.Features.Image.Ingest;
using API.Features.OriginStrategies.Credentials;
using API.Infrastructure;
using API.Infrastructure.Messaging;
using API.Infrastructure.Validation;
using API.Settings;
using DLCS.Core.Caching;
using DLCS.Core.Encryption;
using DLCS.Core.Settings;
using DLCS.Model.Messaging;
using DLCS.Repository;
using DLCS.Repository.Messaging;
using DLCS.Repository.NamedQueries;
using DLCS.Repository.NamedQueries.Infrastructure;
using DLCS.Web.Auth;
using DLCS.Web.Configuration;
using DLCS.Web.Handlers;
using DLCS.Web.Logging;
using FluentValidation;
using Hydra;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace API;

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
            .AddOptions<ApiSettings>().Bind(configuration)
            .ValidateFluentValidation()
            .ValidateOnStart();

        services
            .Configure<NamedQueryTemplateSettings>(configuration)
            .Configure<DlcsSettings>(configuration.GetSection("DLCS"))
            .Configure<CacheSettings>(cachingSection);

        var apiSettings = configuration.Get<ApiSettings>();
        var cacheSettings = cachingSection.Get<CacheSettings>();

        services
            .AddHttpContextAccessor()
            .AddSingleton<CredentialsExporter>()
            .AddSingleton<ApiKeyGenerator>()
            .AddSingleton<IEncryption, SHA256>()
            .AddSingleton<JwtAuthHelper>()
            .AddSingleton<DlcsApiAuth>()
            .AddTransient<ClaimsPrincipal>(s => s.GetRequiredService<IHttpContextAccessor>().HttpContext.User)
            .AddCaching(cacheSettings)
            .AddDataAccess(configuration)
            .AddScoped<IIngestNotificationSender, IngestNotificationSender>()
            .AddScoped<IAssetNotificationSender, AssetNotificationSender>()
            .AddScoped<AssetProcessor>()
            .AddScoped<IBulkAssetPatcher, BulkAssetPatcher>()
            .AddScoped<DeliveryChannelProcessor>()
            .AddTransient<TimingHandler>()
            .AddScoped<DeliveryChannelPolicyDataValidator>()
            .AddValidatorsFromAssemblyContaining<Startup>()
            .ConfigureMediatR()
            .AddNamedQueriesCore()
            .AddAws(configuration, webHostEnvironment)
            .AddCorrelationIdHeaderPropagation()
            .AddTopicNotifiers()
            .ConfigureSwagger();

        services.AddHttpClient<IEngineClient, EngineClient>(client =>
            {
                client.BaseAddress = apiSettings.DLCS.EngineRoot;
            })
            .AddHttpMessageHandler<TimingHandler>();

        services.AddDlcsBasicAuth(options =>
            {
                options.Realm = "DLCS-API";
                options.Salt = apiSettings.ApiSalt;
            });
        
        services.AddCors(options =>
        {
            options.AddPolicy("CorsPolicy",
                builder => builder
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .SetIsOriginAllowed(_ => true)
                    .AllowCredentials());
        });

        services
            .AddControllers()
            .AddNewtonsoftJson(options =>
            {
                options.SerializerSettings.ApplyHydraSerializationSettings();
            });

        services.AddSwaggerGenNewtonsoftSupport();

        services
            .AddHealthChecks()
            .AddDbContextCheck<DlcsContext>("DLCS-DB");

        services
            .Configure<KestrelServerOptions>(options =>
            {
                options.Limits.MaxRequestBodySize = 100_000_000; // if don't set default value is: 30 MB
            })
            .ConfigureForwardedHeaders(configuration);
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILogger<Startup> logger)
    {
        app.UseForwardedHeaders();
        if (env.IsDevelopment())
        {
            DlcsContextConfiguration.TryRunMigrations(configuration, logger);
            app.UseDeveloperExceptionPage();
        }

        var applicationOptions = configuration.Get<ApiSettings>();
        var pathBase = applicationOptions.PathBase;

        app
            .HandlePathBase(pathBase, logger)
            .UseSwaggerWithUI("DLCS API", pathBase, "v2")
            .UseRouting()
            .UseSerilogRequestLogging(opts =>
            {
                opts.GetLevel = LogHelper.ExcludeHealthChecks;
            })
            .UseCors("CorsPolicy")
            .UseAuthentication()
            .UseAuthorization()
            .UseEndpoints(endpoints =>
            {
                endpoints
                    .MapControllers()
                    .RequireAuthorization();
                endpoints.MapHealthChecks("/ping").AllowAnonymous();
            });
    }
}
