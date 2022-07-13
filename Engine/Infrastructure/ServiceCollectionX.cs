using DLCS.AWS.Configuration;
using DLCS.AWS.S3;
using DLCS.AWS.SQS;
using DLCS.Repository;
using Engine.Ingest;
using Engine.Ingest.Handlers;
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
        => services.AddScoped<IAssetIngester, AssetIngester>();

    /// <summary>
    /// Add HealthChecks for Database and Queues
    /// </summary>
    public static IServiceCollection ConfigureHealthChecks(this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddHealthChecks()
            .AddNpgSql(configuration.GetPostgresSqlConnection())
            .AddQueueHealthCheck();

        return services;
    }
}