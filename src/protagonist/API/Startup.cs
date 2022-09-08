using System.Security.Claims;
using API.Auth;
using API.Features.Assets;
using API.Infrastructure;
using API.Settings;
using DLCS.AWS.Configuration;
using DLCS.AWS.S3;
using DLCS.AWS.SQS;
using DLCS.Core.Caching;
using DLCS.Core.Encryption;
using DLCS.Core.Handlers;
using DLCS.Core.Settings;
using DLCS.Model;
using DLCS.Model.Assets;
using DLCS.Model.Auth;
using DLCS.Model.Customers;
using DLCS.Model.Messaging;
using DLCS.Model.Policies;
using DLCS.Model.Processing;
using DLCS.Model.Spaces;
using DLCS.Model.Storage;
using DLCS.Repository;
using DLCS.Repository.Assets;
using DLCS.Repository.Auth;
using DLCS.Repository.Customers;
using DLCS.Repository.Entities;
using DLCS.Repository.Messaging;
using DLCS.Repository.Policies;
using DLCS.Repository.Spaces;
using DLCS.Repository.Storage;
using DLCS.Web.Auth;
using DLCS.Web.Configuration;
using FluentValidation;
using Hydra;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
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
        services.Configure<ApiSettings>(configuration);
        services.Configure<DlcsSettings>(configuration.GetSection("DLCS"));
        var cachingSection = configuration.GetSection("Caching");
        services.Configure<CacheSettings>(cachingSection);

        var apiSettings = configuration.Get<ApiSettings>();
        var cacheSettings = cachingSection.Get<CacheSettings>();
        
        services
            .AddHttpContextAccessor()
            .AddSingleton<IEncryption, SHA256>()
            .AddSingleton<DeliveratorApiAuth>()
            .AddTransient<ClaimsPrincipal>(s => s.GetRequiredService<IHttpContextAccessor>().HttpContext.User)
            .AddMemoryCache(memoryCacheOptions =>
            {
                memoryCacheOptions.SizeLimit = cacheSettings.MemoryCacheSizeLimit;
                memoryCacheOptions.CompactionPercentage = cacheSettings.MemoryCacheCompactionPercentage;
            })
            .AddLazyCache()
            .AddDlcsContext(configuration)
            .AddScoped<IAssetRepository, AssetRepository>()
            .AddScoped<IApiAssetRepository>(provider =>
                ActivatorUtilities.CreateInstance<ApiAssetRepository>(
                    provider,
                    provider.GetRequiredService<IAssetRepository>()))
            .AddScoped<ISpaceRepository, SpaceRepository>()
            .AddScoped<IBatchRepository, BatchRepository>()
            .AddScoped<IEntityCounterRepository, EntityCounterRepository>()
            .AddScoped<ICustomerQueueRepository, CustomerQueueRepository>()
            .AddScoped<IStorageRepository, CustomerStorageRepository>()
            // Do not use a DlcsContext, _may_ be Singleton (but should they)
            .AddSingleton<ICustomerRepository, DapperCustomerRepository>()
            .AddSingleton<IAuthServicesRepository, DapperAuthServicesRepository>()
            .AddScoped<IPolicyRepository, PolicyRepository>()
            .AddScoped<IAssetNotificationSender, AssetNotificationSender>()
            .AddTransient<TimingHandler>()
            .AddValidatorsFromAssemblyContaining<Startup>()
            .ConfigureMediatR()
            .ConfigureSwagger();

        services
            .AddSingleton<IBucketReader, S3BucketReader>()
            .AddSingleton<IBucketWriter, S3BucketWriter>()
            .AddSingleton<IStorageKeyGenerator, S3StorageKeyGenerator>()
            .AddSingleton<IQueueLookup, SqsQueueLookup>()
            .AddSingleton<IQueueSender, SqsQueueSender>()
            .AddSingleton<SqsQueueUtilities>()
            .SetupAWS(configuration, webHostEnvironment)
            .WithAmazonS3()
            .WithAmazonSQS();

        services.AddHttpClient<IEngineClient, EngineClient>()
            .AddHttpMessageHandler<TimingHandler>();

        services.AddDlcsBasicAuth(options =>
            {
                options.Realm = "DLCS-API";
                options.Salt = apiSettings.Salt;
            });
        
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
            .AddControllers()
            .AddNewtonsoftJson(options =>
            {
                options.SerializerSettings.ApplyHydraSerializationSettings();
            });

        services
            .AddHealthChecks()
            .AddDbContextCheck<DlcsContext>("DLCS-DB");
        
        services.Configure<KestrelServerOptions>(options =>
        {
            options.Limits.MaxRequestBodySize = 100_000_000; // if don't set default value is: 30 MB
        });
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILogger<Startup> logger)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        var applicationOptions = configuration.Get<ApiSettings>();
        var pathBase = applicationOptions.PathBase;

        app
            .HandlePathBase(pathBase, logger)
            .UseSwaggerWithUI("DLCS API", pathBase, "v2")
            .UseRouting()
            .UseSerilogRequestLogging()
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