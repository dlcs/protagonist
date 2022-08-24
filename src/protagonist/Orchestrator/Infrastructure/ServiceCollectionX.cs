using DLCS.AWS.Configuration;
using DLCS.AWS.S3;
using DLCS.Core.Caching;
using DLCS.Core.Encryption;
using DLCS.Core.FileSystem;
using DLCS.Model.Assets;
using DLCS.Model.Assets.CustomHeaders;
using DLCS.Model.Auth;
using DLCS.Model.Customers;
using DLCS.Model.PathElements;
using DLCS.Model.Policies;
using DLCS.Repository;
using DLCS.Repository.Assets;
using DLCS.Repository.Assets.CustomHeaders;
using DLCS.Repository.Auth;
using DLCS.Repository.Customers;
using DLCS.Repository.Policies;
using DLCS.Web.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orchestrator.Assets;
using Orchestrator.Features.Images.ImageServer;
using Orchestrator.Features.Images.Orchestration;
using Orchestrator.Infrastructure.Deliverator;
using Orchestrator.Infrastructure.ReverseProxy;
using Orchestrator.Settings;

namespace Orchestrator.Infrastructure;

public static class ServiceCollectionX
{
    /// <summary>
    /// Add all dataaccess dependencies 
    /// </summary>
    public static IServiceCollection AddDataAccess(this IServiceCollection services, IConfiguration configuration)
        => services
            .AddSingleton<ICustomerRepository, DapperCustomerRepository>()
            .AddSingleton<IPathCustomerRepository, CustomerPathElementRepository>()
            .AddSingleton<IAssetRepository, DapperAssetRepository>()
            .AddSingleton<IThumbRepository, ThumbRepository>()
            .AddScoped<IPolicyRepository, PolicyRepository>()
            .AddSingleton<ICredentialsRepository, DapperCredentialsRepository>()
            .AddSingleton<IAuthServicesRepository, DapperAuthServicesRepository>()
            .AddSingleton<ICustomHeaderRepository, DapperCustomHeaderRepository>()
            .AddScoped<ICustomerOriginStrategyRepository, CustomerOriginStrategyRepository>()
            .AddDlcsContext(configuration);

    /// <summary>
    /// Add required caching dependencies
    /// </summary>
    public static IServiceCollection AddCaching(this IServiceCollection services, CacheSettings cacheSettings)
        => services
            .AddMemoryCache(memoryCacheOptions =>
            {
                memoryCacheOptions.SizeLimit = cacheSettings.MemoryCacheSizeLimit;
                memoryCacheOptions.CompactionPercentage = cacheSettings.MemoryCacheCompactionPercentage;
            })
            .AddLazyCache();

    /// <summary>
    /// Add DLCS API Client dependencies
    /// </summary>
    /// <returns></returns>
    public static IServiceCollection AddApiClient(this IServiceCollection services,
        OrchestratorSettings orchestratorSettings)
    {
        var apiRoot = orchestratorSettings.ApiRoot;
        services
            .AddSingleton<DeliveratorApiAuth>()
            .AddSingleton<IEncryption, SHA256>()
            .AddHttpClient<IDlcsApiClient, DeliveratorApiClient>(client =>
            {
                client.DefaultRequestHeaders.WithRequestedBy();
                client.BaseAddress = apiRoot;
            });

        return services;
    }
    
    /// <summary>
    /// Add ImageServerClient dependencies for building and managing info.json requests.
    /// </summary>
    /// <returns></returns>
    public static IServiceCollection AddInfoJsonClient(this IServiceCollection services)
    {
        services
            .AddScoped<InfoJsonConstructor>()
            .AddScoped<InfoJsonService>()
            .AddHttpClient<IImageServerClient, YarpImageServerClient>(client =>
            {
                client.DefaultRequestHeaders.WithRequestedBy();
            });

        return services;
    }

    /// <summary>
    /// Add required caching dependencies
    /// </summary>
    public static IServiceCollection AddOrchestration(this IServiceCollection services,
        OrchestratorSettings settings)
    {
        var serviceCollection = services
            .AddSingleton<IAssetTracker, MemoryAssetTracker>()
            .AddSingleton<IImageOrchestrator, ImageOrchestrator>()
            .AddSingleton<IFileSystem, FileSystem>()
            .AddSingleton<IOrchestrationQueue>(sp =>
                ActivatorUtilities.CreateInstance<BoundedChannelOrchestrationQueue>(sp,
                    settings.OrchestrateOnInfoJsonMaxCapacity));

        if (settings.OrchestrateOnInfoJson)
            serviceCollection.AddHostedService<OrchestrationQueueMonitor>();
        
        return serviceCollection;
    }

    /// <summary>
    /// Add HealthChecks for Database and downstream image-servers
    /// </summary>
    public static IServiceCollection ConfigureHealthChecks(this IServiceCollection services,
        IConfigurationSection proxySection,
        IConfiguration configuration)
    {
        var proxy = proxySection.Get<ProxySettings>();
        var tagsList = new[] { "detail-only" };
        var healthChecksBuilder = services
            .AddHealthChecks()
            .AddNpgSql(configuration.GetPostgresSqlConnection(), name: "Database")
            .AddProxyDestination(ProxyDestination.ImageServer, "Image Server")
            .AddProxyDestination(ProxyDestination.Thumbs, "Thumbs", tags: tagsList);
            
        if (proxy.CanResizeThumbs)
        {
            healthChecksBuilder.AddProxyDestination(ProxyDestination.ResizeThumbs, "ThumbsResize", tags: tagsList);
        }

        return services;
    }

    /// <summary>
    /// Add required AWS services
    /// </summary>
    public static IServiceCollection AddAws(this IServiceCollection services,
        IConfiguration configuration, IWebHostEnvironment webHostEnvironment)
    {
        services
            .AddSingleton<IBucketReader, S3BucketReader>()
            .AddSingleton<IBucketWriter, S3BucketWriter>()
            .AddSingleton<IStorageKeyGenerator, S3StorageKeyGenerator>()
            .SetupAWS(configuration, webHostEnvironment)
            .WithAmazonS3();

        return services;
    }
}