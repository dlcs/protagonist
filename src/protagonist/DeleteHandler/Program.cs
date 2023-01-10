using DLCS.AWS.Configuration;
using DLCS.AWS.S3;
using DLCS.AWS.Settings;
using DLCS.AWS.SQS;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;

namespace DeleteHandler;

public class Program
{
    public static async Task Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .CreateLogger();

        try
        {
            await CreateHostBuilder(args).Build().RunAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application start-up failed");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                services
                    .AddAws(hostContext.Configuration, hostContext.HostingEnvironment);
            })
            .UseSerilog((hostingContext, loggerConfiguration)
                => loggerConfiguration
                    .ReadFrom.Configuration(hostingContext.Configuration)
            );
}

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
            .AddSingleton<IStorageKeyGenerator, S3StorageKeyGenerator>()
            .AddSingleton<SqsListenerManager>()
            .AddTransient(typeof(SqsListener<>))
            .AddSingleton<SqsQueueUtilities>()
            .SetupAWS(configuration, hostEnvironment)
            .WithAmazonS3()
            .WithAmazonSQS();
        
        return services;
    }

    public static IServiceCollection AddQueueMonitoring(this IServiceCollection services)
        => services
            .AddScoped<AssetDeletedHandler>()
            .AddDefaultQueueHandler<AssetDeletedHandler>()
            .AddHostedService<DeleteQueueMonitor>();
}

public class AssetDeletedHandler : IMessageHandler
{
    public Task<bool> HandleMessage(QueueMessage message, CancellationToken cancellationToken = default)
    {
        /*
         * Delete storage jp2
         * Delete thumbnails
         * Delete from hot-disk
         * Purge varnish?
         * Purge CF?
         */
        throw new NotImplementedException();
    }
}

public class DeleteQueueMonitor : BackgroundService
{
    private readonly IHostApplicationLifetime hostApplicationLifetime;
    private readonly IOptions<AWSSettings> awsSettings;
    private readonly SqsListenerManager sqsListenerManager;
    private readonly ILogger<DeleteQueueMonitor> logger;

    public DeleteQueueMonitor(SqsListenerManager sqsListenerManager, ILogger<DeleteQueueMonitor> logger,
        IHostApplicationLifetime hostApplicationLifetime, IOptions<AWSSettings> awsSettings)
    {
        this.sqsListenerManager = sqsListenerManager;
        this.logger = logger;
        this.hostApplicationLifetime = hostApplicationLifetime;
        this.awsSettings = awsSettings;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting DeleteQueueMonitor");
        
        await sqsListenerManager.SetupDefaultQueue(awsSettings.Value.SQS.DeleteNotificationQueueName);
        hostApplicationLifetime.ApplicationStopping.Register(OnStopping);
    }
    
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogWarning("Stopping DeleteQueueMonitor");
        return Task.CompletedTask;
    }
    
    private void OnStopping()
    {
        sqsListenerManager.StopListening();
        logger.LogInformation("Stopping listening to queues");
    }
}

public class DeleteHandlerSettings
{
    public string? ImageFolderTemplate { get; set; }
}