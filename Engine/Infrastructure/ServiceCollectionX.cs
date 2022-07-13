using DLCS.AWS.Configuration;
using DLCS.AWS.S3;
using DLCS.AWS.SQS;
using DLCS.Model.Assets;
using DLCS.Model.Policies;
using DLCS.Repository;
using DLCS.Repository.Caching;
using DLCS.Repository.Policies;
using Engine.Ingest;
using Engine.Ingest.Handlers;
using Engine.Ingest.Workers;
using Engine.Messaging;

namespace Engine.Infrastructure;

public static class ServiceCollectionX
{
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
            .WithAmazonS3()
            .WithAmazonSQS();

        return services;
    }

    /// <summary>
    /// Configure listeners for queues
    /// </summary>
    public static IServiceCollection AddQueueMonitoring(this IServiceCollection services)
        => services
            .AddSingleton<SqsListenerManager>()
            .AddTransient(typeof(SqsListener<>))
            .AddSingleton<QueueHandlerResolver<EngineMessageType>>(provider => messageType => messageType switch
            {
                EngineMessageType.Ingest => provider.GetRequiredService<IngestHandler>(),
                EngineMessageType.TranscodeComplete => provider.GetRequiredService<TranscodeCompletionHandler>(),
                _ => throw new ArgumentOutOfRangeException(nameof(messageType), messageType, null)
            })
            .AddTransient<IngestHandler>()
            .AddTransient<TranscodeCompletionHandler>()
            .AddSingleton<SqsQueueUtilities>()
            .AddHostedService<SqsListenerService>();

    /// <summary>
    /// Adds all asset ingestion classes and related dependencies. 
    /// </summary>
    public static IServiceCollection AddAssetIngestion(this IServiceCollection services)
        => services
            .AddScoped<IAssetIngester, AssetIngester>()
            .AddTransient<ImageIngesterWorker>()
            .AddTransient<IngestorResolver>(provider => family => family switch
            {
                AssetFamily.Image => provider.GetRequiredService<ImageIngesterWorker>(),
                AssetFamily.Timebased => throw new NotImplementedException("Not yet"),
                AssetFamily.File => throw new NotImplementedException("File shouldn't be here"),
                _ => throw new KeyNotFoundException("Attempt to resolve ingestor handler for unknown family")
            });

    /// <summary>
    /// Add all dataaccess dependencies, including repositories and DLCS context 
    /// </summary>
    public static IServiceCollection AddDataAccess(this IServiceCollection services, IConfiguration configuration)
        => services
            .AddScoped<IPolicyRepository, PolicyRepository>()
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
    /// Add HealthChecks for Database and Queues
    /// </summary>
    public static IServiceCollection ConfigureHealthChecks(this IServiceCollection services)
    {
        services
            .AddHealthChecks()
            .AddDbContextCheck<DlcsContext>("DLCS-DB")
            .AddQueueHealthCheck();

        return services;
    }
}