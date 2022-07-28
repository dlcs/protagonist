using Amazon.ElasticTranscoder;
using DLCS.AWS.Configuration;
using DLCS.AWS.S3;
using DLCS.AWS.SQS;
using DLCS.Core.FileSystem;
using DLCS.Model.Assets;
using DLCS.Model.Auth;
using DLCS.Model.Customers;
using DLCS.Model.Policies;
using DLCS.Model.Storage;
using DLCS.Repository;
using DLCS.Repository.Auth;
using DLCS.Repository.Caching;
using DLCS.Repository.Customers;
using DLCS.Repository.Policies;
using DLCS.Repository.Storage;
using DLCS.Repository.Strategy.DependencyInjection;
using Engine.Ingest;
using Engine.Ingest.Completion;
using Engine.Ingest.Handlers;
using Engine.Ingest.Image;
using Engine.Ingest.Image.Appetiser;
using Engine.Ingest.Timebased;
using Engine.Ingest.Workers;
using Engine.Ingest.Workers.Persistence;
using Engine.Messaging;
using Engine.Settings;

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
            .WithAmazonSQS()
            .WithAWSService<IAmazonElasticTranscoder>();

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
    public static IServiceCollection AddAssetIngestion(this IServiceCollection services, EngineSettings engineSettings)
    {
        services
            .AddScoped<IAssetIngester, AssetIngester>()
            .AddScoped<TimebasedIngesterWorker>()
            .AddScoped<ImageIngesterWorker>()
            .AddSingleton<IThumbCreator, ThumbCreator>()
            .AddTransient<IngestorResolver>(provider => family => family switch
            {
                AssetFamily.Image => provider.GetRequiredService<ImageIngesterWorker>(),
                AssetFamily.Timebased => provider.GetRequiredService<TimebasedIngesterWorker>(),
                AssetFamily.File => throw new NotImplementedException("File shouldn't be here"),
                _ => throw new KeyNotFoundException("Attempt to resolve ingestor handler for unknown family")
            })
            .AddSingleton<IFileSystem, FileSystem>()
            .AddSingleton<IMediaTranscoder, ElasticTranscoder>()
            .AddScoped<IAssetToDisk, AssetToDisk>()
            .AddScoped<IImageIngestorCompletion, ImageIngestorCompletion>()
            .AddScoped<IEngineAssetRepository, EngineAssetRepository>()
            .AddScoped<AssetToS3>()
            .AddOriginStrategies();

        services.AddHttpClient<IImageProcessor, AppetiserClient>(client =>
        {
            client.BaseAddress = engineSettings.ImageIngest.ImageProcessorUrl;
            client.Timeout = TimeSpan.FromMilliseconds(engineSettings.ImageIngest.ImageProcessorTimeoutMs);
        });

        services.AddHttpClient<OrchestratorClient>(client =>
        {
            client.BaseAddress = engineSettings.OrchestratorBaseUrl;
            client.Timeout = TimeSpan.FromMilliseconds(engineSettings.OrchestratorTimeoutMs);
        });

        return services;
    }

    /// <summary>
    /// Add all dataaccess dependencies, including repositories and DLCS context 
    /// </summary>
    public static IServiceCollection AddDataAccess(this IServiceCollection services, IConfiguration configuration)
        => services
            .AddScoped<IPolicyRepository, PolicyRepository>()
            .AddScoped<ICustomerOriginStrategyRepository, CustomerOriginStrategyRepository>()
            .AddSingleton<ICredentialsRepository, DapperCredentialsRepository>()
            .AddScoped<IStorageRepository, CustomerStorageRepository>()
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