using DLCS.AWS.Configuration;
using DLCS.AWS.S3;
using DLCS.Model.Assets;
using DLCS.Repository.Assets;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Thumbs.Infrastructure;

public static class ServiceCollectionX
{
    /// <summary>
    /// Add required dependencies for handling thumbnails
    /// </summary>
    public static IServiceCollection AddThumbnailHandling(this IServiceCollection services) 
        => services
            .AddSingleton<ThumbnailHandler>()
            .AddSingleton<IThumbRepository, ThumbRepository>();

    /// <summary>
    /// Configure AWS dependencies
    /// </summary>
    public static IServiceCollection AddAws(this IServiceCollection services, IConfiguration configuration,
        IWebHostEnvironment webHostEnvironment)
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