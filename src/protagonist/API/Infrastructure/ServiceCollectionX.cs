using System.IO;
using System.Reflection;
using API.Features.Assets;
using API.Features.Customer;
using API.Features.DeliveryChannels;
using API.Features.DeliveryChannels.DataAccess;
using API.Infrastructure.Requests.Pipelines;
using DLCS.AWS.Configuration;
using DLCS.AWS.ElasticTranscoder;
using DLCS.AWS.S3;
using DLCS.AWS.SNS;
using DLCS.AWS.SQS;
using DLCS.Core.Caching;
using DLCS.Mediatr.Behaviours;
using DLCS.Model;
using DLCS.Model.Assets;
using DLCS.Model.Auth;
using DLCS.Model.Customers;
using DLCS.Model.DeliveryChannels;
using DLCS.Model.PathElements;
using DLCS.Model.Policies;
using DLCS.Model.Processing;
using DLCS.Model.Spaces;
using DLCS.Model.Storage;
using DLCS.Repository;
using DLCS.Repository.Assets;
using DLCS.Repository.Auth;
using DLCS.Repository.Customers;
using DLCS.Repository.Entities;
using DLCS.Repository.Policies;
using DLCS.Repository.Processing;
using DLCS.Repository.Spaces;
using DLCS.Repository.Storage;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;

namespace API.Infrastructure;

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
    /// Add MediatR services and pipeline behaviours to service collection.
    /// </summary>
    public static IServiceCollection ConfigureMediatR(this IServiceCollection services)
        => services
            .AddMediatR(typeof(Startup))
            .AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>))
            .AddScoped(typeof(IPipelineBehavior<,>), typeof(CacheInvalidationBehaviour<,>));
    
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
            .AddSingleton<IQueueLookup, SqsQueueLookup>()
            .AddSingleton<IQueueSender, SqsQueueSender>()
            .AddScoped<ITopicPublisher, TopicPublisher>()
            .AddSingleton<IPathCustomerRepository, CustomerPathElementRepository>()
            .AddSingleton<SqsQueueUtilities>()
            .AddSingleton<IElasticTranscoderWrapper, ElasticTranscoderWrapper>()
            .SetupAWS(configuration, webHostEnvironment)
            .WithAmazonS3()
            .WithAmazonSQS()
            .WithAmazonSNS()
            .WithAmazonElasticTranscoder();

        return services;
    }
    
    /// <summary>
    /// Add all dataaccess dependencies, including repositories and DLCS context 
    /// </summary>
    public static IServiceCollection AddDataAccess(this IServiceCollection services, IConfiguration configuration)
        => services
            .AddDlcsContext(configuration)
            .AddSingleton<AssetCachingHelper>()
            .AddScoped<IApiAssetRepository, ApiAssetRepository>()
            .AddScoped<ISpaceRepository, SpaceRepository>()
            .AddScoped<IBatchRepository, BatchRepository>()
            .AddScoped<IEntityCounterRepository, EntityCounterRepository>()
            .AddScoped<ICustomerQueueRepository, CustomerQueueRepository>()
            .AddScoped<IStorageRepository, CustomerStorageRepository>()
            .AddSingleton<ICustomerRepository, DapperCustomerRepository>()
            .AddSingleton<IAuthServicesRepository, DapperAuthServicesRepository>()
            .AddScoped<IPolicyRepository, PolicyRepository>()
            .AddScoped<DapperNewCustomerDeliveryChannelRepository>()
            .AddScoped<IDefaultDeliveryChannelRepository, DefaultDeliveryChannelRepository>()
            .AddScoped<IDeliveryChannelPolicyRepository, DeliveryChannelPolicyRepository>()
            .AddScoped<IAvChannelPolicyOptionsRepository, AvChannelPolicyOptionsRepository>()
            .AddDlcsContext(configuration);

    /// <summary>
    /// Add SwaggerGen services to service collection.
    /// </summary>
    public static IServiceCollection ConfigureSwagger(this IServiceCollection services)
        => services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v2", new OpenApiInfo
            {
                Title = "DLCS API", 
                Version = "v2",
                Description = "API for interacting with DLCS"
            });

            c.AddSecurityDefinition(
                "basic", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "basic",
                    In = ParameterLocation.Header,
                    Description = "Basic Authorization header using the Bearer scheme.",
                });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "basic",
                        },
                        Scheme = "basic",
                        Name = "Authorization",
                        In = ParameterLocation.Header
                    },
                    new string[] { }
                },
            });
            
            // Set the comments path for the Swagger JSON and UI.
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            c.IncludeXmlComments(xmlPath);
        });
}