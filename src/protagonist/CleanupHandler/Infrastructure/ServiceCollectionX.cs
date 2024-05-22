using CleanupHandler.Repository;
using DLCS.AWS.Cloudfront;
using DLCS.AWS.Configuration;
using DLCS.AWS.ElasticTranscoder;
using DLCS.AWS.S3;
using DLCS.AWS.SQS;
using DLCS.Core.Caching;
using DLCS.Core.FileSystem;
using DLCS.Model.Assets;
using DLCS.Model.Assets.Metadata;
using DLCS.Repository;
using DLCS.Repository.Assets;
using DLCS.Repository.Messaging;
using DLCS.Web.Handlers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CleanupHandler.Infrastructure;

public static class ServiceCollectionX
{
    /// <summary>
    /// Configure AWS services. Generic, non project-specific
    /// </summary>
    public static IServiceCollection AddAws(this IServiceCollection services,
        IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        services
            .AddSingleton<IBucketWriter, S3BucketWriter>()
            .AddSingleton<IBucketReader, S3BucketReader>()
            .AddSingleton<IStorageKeyGenerator, S3StorageKeyGenerator>()
            .AddSingleton<IQueueLookup, SqsQueueLookup>()
            .AddSingleton<IQueueSender, SqsQueueSender>()
            .AddSingleton<SqsListenerManager>()
            .AddTransient(typeof(SqsListener<>))
            .AddSingleton<ICacheInvalidator, CloudfrontInvalidator>()
            .AddSingleton<IElasticTranscoderWrapper, ElasticTranscoderWrapper>()
            .AddSingleton<SqsQueueUtilities>()
            .SetupAWS(configuration, hostEnvironment)
            .WithAmazonS3()
            .WithAmazonCloudfront()
            .WithAmazonSQS()
            .WithAmazonElasticTranscoder();

        return services;
    }

    /// <summary>
    /// Configure BackgroundWorker + handler services
    /// </summary>
    public static IServiceCollection AddQueueMonitoring(this IServiceCollection services)
        => services
            .AddScoped<QueueHandlerResolver<AssetQueueType>>(provider => messageType => messageType switch
            {
                AssetQueueType.Delete => provider.GetRequiredService<AssetDeletedHandler>(),
                AssetQueueType.Update => provider.GetRequiredService<AssetUpdatedHandler>(),
                _ => throw new ArgumentOutOfRangeException(nameof(messageType), messageType, null)
            })
            .AddScoped<AssetDeletedHandler>()
            .AddScoped<AssetUpdatedHandler>()
            .AddSingleton<IFileSystem, FileSystem>()
            .AddHostedService<CleanupHandlerQueueMonitor>();

    /// <summary>
    /// Add all data access dependencies, including repositories and DLCS context 
    /// </summary>
    public static IServiceCollection AddDataAccess(this IServiceCollection services, IConfiguration configuration, 
        CleanupHandlerSettings cleanupHandlerSettings)
    {
        services
            .AddSingleton<IAssetRepository, DapperAssetRepository>()
            .AddScoped<IAssetApplicationMetadataRepository, AssetApplicationMetadataRepository>()
            .AddSingleton<IThumbRepository, ThumbRepository>()
            .AddSingleton<AssetCachingHelper>()
            .AddTransient<TimingHandler>()
            .AddScoped<ICleanupHandlerAssetRepository, CleanupHandlerAssetRepository>()
            .AddDlcsContext(configuration);

        services.AddHttpClient<IEngineClient, EngineClient>(client =>
            {
                client.BaseAddress = cleanupHandlerSettings.AssetModifiedSettings.EngineRoot;
            })
            .AddHttpMessageHandler<TimingHandler>();

        return services;
    }

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
}
