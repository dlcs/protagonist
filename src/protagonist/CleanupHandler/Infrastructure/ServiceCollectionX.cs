using DLCS.AWS.Cloudfront;
using DLCS.AWS.Configuration;
using DLCS.AWS.S3;
using DLCS.AWS.SQS;
using DLCS.Core.Caching;
using DLCS.Core.FileSystem;
using DLCS.Model.Customers;
using DLCS.Repository.Customers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CleanupHandler.Infrastructure;

public static class ServiceCollectionX
{
    /// <summary>
    /// Configure caching
    /// </summary>
    public static IServiceCollection AddCaching(this IServiceCollection services, CacheSettings cacheSettings)
        => services.AddMemoryCache(memoryCacheOptions =>
            {
                memoryCacheOptions.SizeLimit = cacheSettings.MemoryCacheSizeLimit;
                memoryCacheOptions.CompactionPercentage = cacheSettings.MemoryCacheCompactionPercentage;
            })
            .AddLazyCache();
    
    /// <summary>
    /// Configure AWS services. Generic, non project-specific
    /// </summary>
    public static IServiceCollection AddAws(this IServiceCollection services,
        IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        services
            .AddSingleton<IBucketWriter, S3BucketWriter>()
            .AddSingleton<IStorageKeyGenerator, S3StorageKeyGenerator>()
            .AddSingleton<SqsListenerManager>()
            .AddTransient(typeof(SqsListener<>))
            .AddSingleton<ICacheInvalidator, CloudfrontInvalidator>()
            .AddSingleton<SqsQueueUtilities>()
            .SetupAWS(configuration, hostEnvironment)
            .WithAmazonS3()
            .WithAmazonCloudfront()
            .WithAmazonSQS();
        
        return services;
    }

    /// <summary>
    /// Add all dataaccess dependencies, including repositories and DLCS context 
    /// </summary>
    public static IServiceCollection AddDataAccess(this IServiceCollection services, IConfiguration configuration)
        => services
            .AddSingleton<ICustomerRepository, DapperCustomerRepository>();

    /// <summary>
    /// Configure BackgroundWorker + handler services
    /// </summary>
    public static IServiceCollection AddQueueMonitoring(this IServiceCollection services)
        => services
            .AddScoped<AssetDeletedHandler>()
            .AddDefaultQueueHandler<AssetDeletedHandler>()
            .AddSingleton<IFileSystem, FileSystem>()
            .AddHostedService<DeleteQueueMonitor>();
}