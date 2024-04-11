using DLCS.AWS.Configuration;
using DLCS.AWS.S3;
using DLCS.Model.Assets;
using DLCS.Model.Assets.Metadata;
using DLCS.Model.Policies;
using DLCS.Repository.Assets;
using DLCS.Repository.Policies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Thumbs.Reorganising;
using Thumbs.Settings;

namespace Thumbs.Infrastructure;

public static class ServiceCollectionX
{
    /// <summary>
    /// Add required dependencies for handling thumbnails
    /// </summary>
    public static IServiceCollection AddThumbnailHandling(this IServiceCollection services, ThumbsSettings settings)
    {
        services
            .AddSingleton<ThumbnailHandler>();

        if (settings.EnsureNewThumbnailLayout)
        {
            // If reorganising thumb, register IThumbReorganiser and ReorganisingThumbRepository which is a
            // decorator for default IThumbRespositry
            Log.Information("Thumbs supports reorganising thumbs");
            services
                .AddSingleton<IThumbnailPolicyRepository, DapperThumbnailPolicy>()
                .AddScoped<IAssetApplicationMetadataRepository, AssetApplicationMetadataRepository>()
                .AddSingleton<ThumbRepository>()
                .AddSingleton<IThumbRepository>(provider =>
                    ActivatorUtilities.CreateInstance<ReorganisingThumbRepository>(
                        provider,
                        provider.GetRequiredService<ThumbRepository>()))
                .AddSingleton<IThumbReorganiser, ThumbReorganiser>();
        }
        else
        {
            // If reorganising not supported only register standard IThumbRepository
            services.AddSingleton<IThumbRepository, ThumbRepository>();
        }

        return services;
    }

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