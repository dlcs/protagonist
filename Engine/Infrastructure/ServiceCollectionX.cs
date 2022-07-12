using DLCS.AWS.Configuration;
using DLCS.AWS.S3;
using DLCS.AWS.SQS;
using Engine.Ingest.Handlers;
using Engine.Messaging;

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
            .AddTransient<IngestHandler>()
            .AddTransient<TranscodeCompletionHandler>()
            .AddSingleton<SqsQueueUtilities>()
            .AddHostedService<SqsListenerService>();
}