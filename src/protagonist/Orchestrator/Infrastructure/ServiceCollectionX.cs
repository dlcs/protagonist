﻿using System;
using DLCS.AWS.Configuration;
using DLCS.AWS.S3;
using DLCS.Core.Caching;
using DLCS.Core.Encryption;
using DLCS.Core.FileSystem;
using DLCS.Model.Assets;
using DLCS.Model.Assets.CustomHeaders;
using DLCS.Model.Auth;
using DLCS.Model.Customers;
using DLCS.Model.PathElements;
using DLCS.Model.Policies;
using DLCS.Repository;
using DLCS.Repository.Assets;
using DLCS.Repository.Assets.CustomHeaders;
using DLCS.Repository.Auth;
using DLCS.Repository.CustomerPath;
using DLCS.Repository.Customers;
using DLCS.Repository.Policies;
using DLCS.Repository.Strategy;
using DLCS.Web.Auth;
using DLCS.Web.Handlers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orchestrator.Assets;
using Orchestrator.Features.Auth;
using Orchestrator.Features.Auth.Paths;
using Orchestrator.Features.Images.ImageServer;
using Orchestrator.Features.Images.Orchestration;
using Orchestrator.Infrastructure.API;
using Orchestrator.Infrastructure.Auth;
using Orchestrator.Infrastructure.Auth.V2;
using Orchestrator.Infrastructure.IIIF;
using Orchestrator.Infrastructure.IIIF.Manifests;
using Orchestrator.Infrastructure.ReverseProxy;
using Orchestrator.Settings;
using ImageServiceVersion = IIIF.ImageApi.Version;
using IIIF2 = IIIF.Presentation.V2;
using IIIF3 = IIIF.Presentation.V3;

namespace Orchestrator.Infrastructure;

/// <summary>
/// Delegate for getting <see cref="IOriginStrategy"/> implementation for specified strategy.
/// </summary>
public delegate IInfoJsonConstructor InfoJsonConstructorResolver(ImageServiceVersion version);

public static class ServiceCollectionX
{
    /// <summary>
    /// Add all dataaccess dependencies 
    /// </summary>
    public static IServiceCollection AddDataAccess(this IServiceCollection services, IConfiguration configuration)
        => services
            .AddSingleton<ICustomerRepository, DapperCustomerRepository>()
            .AddSingleton<IPathCustomerRepository, GranularCustomerPathElementRepository>()
            .AddSingleton<AssetCachingHelper>()
            .AddSingleton<IAssetRepository, DapperAssetRepository>()
            .AddSingleton<IThumbRepository, ThumbRepository>()
            .AddScoped<IPolicyRepository, PolicyRepository>()
            .AddSingleton<ICredentialsRepository, DapperCredentialsRepository>()
            .AddSingleton<IAuthServicesRepository, DapperAuthServicesRepository>()
            .AddSingleton<ICustomHeaderRepository, DapperCustomHeaderRepository>()
            .AddSingleton<ICustomerOriginStrategyRepository, DapperCustomerOriginStrategyRepository>()
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
    /// Add DLCS API Client dependencies
    /// </summary>
    /// <returns></returns>
    public static IServiceCollection AddApiClient(this IServiceCollection services,
        OrchestratorSettings orchestratorSettings)
    {
        var apiRoot = orchestratorSettings.ApiRoot;
        services
            .AddSingleton<DlcsApiAuth>()
            .AddSingleton<IEncryption, SHA256>()
            .AddHttpClient<IDlcsApiClient, ApiClient>(client =>
            {
                client.DefaultRequestHeaders.WithRequestedBy();
                client.BaseAddress = apiRoot;
            });

        return services;
    }
    
    /// <summary>
    /// Add ImageServerClient dependencies for building and managing info.json requests.
    /// </summary>
    public static IServiceCollection AddInfoJsonClient(this IServiceCollection services)
    {
        services
            .AddScoped<IIIFAuth1Builder>()
            .AddScoped<InfoJsonService>()
            .AddScoped<InfoJson2Constructor>()
            .AddScoped<InfoJson3Constructor>()
            .AddScoped<InfoJsonConstructorResolver>(provider => version => version switch
            {
                ImageServiceVersion.V2 => provider.GetRequiredService<InfoJson2Constructor>(),
                ImageServiceVersion.V3 => provider.GetRequiredService<InfoJson3Constructor>(),
                _ => throw new ArgumentOutOfRangeException(nameof(version), version, null)
            })
            .AddHttpClient<IImageServerClient, YarpImageServerClient>(client =>
            {
                client.DefaultRequestHeaders.WithRequestedBy();
            })
            .AddHttpMessageHandler<TimingHandler>();

        return services;
    }

    /// <summary>
    /// Add IIIF Auth services
    /// </summary>
    public static IServiceCollection AddIIIFAuth(this IServiceCollection services,
        OrchestratorSettings orchestratorSettings)
    {
        services
            .AddScoped<AccessChecker>()
            .AddScoped<ISessionAuthService, SessionAuthService>()
            .AddScoped<AuthCookieManager>()
            .AddScoped<Auth2AccessValidator>()
            .AddScoped<Auth1AccessValidator>()
            .AddScoped<IAssetAccessValidator, AssetAccessValidator>()
            .AddScoped<IRoleProviderService, HttpAwareRoleProviderService>()
            .AddScoped<IAuthPathGenerator, ConfigDrivenAuthPathGenerator>()
            .AddScoped<IIIIFAuthBuilder>(provider => provider.GetRequiredService<IIIFAuth2Client>())
            .AddHeaderPropagation(options => options.Headers.Add("Cookie"))
            .AddHttpClient<IIIFAuth2Client>(client =>
            {
                client.DefaultRequestHeaders.WithRequestedBy();
                client.BaseAddress = orchestratorSettings.Auth.Auth2ServiceRoot;
                client.Timeout = TimeSpan.FromSeconds(orchestratorSettings.Auth.AuthTimeoutSecs);
            })
            .AddHeaderPropagation()
            .AddHttpMessageHandler<TimingHandler>();
        return services;
    }

    /// <summary>
    /// Services for building IIIF Manifests 
    /// </summary>
    public static IServiceCollection AddIIIFBuilding(this IServiceCollection services) =>
        services
            .AddScoped<IIIFManifestBuilder>()
            .AddScoped<IThumbSizeProvider, MetadataWithFallbackThumbSizeProvider>()
            .AddScoped<IManifestBuilderUtils, ManifestBuilderUtils>()
            .AddScoped<IBuildManifests<IIIF2.Manifest>, ManifestV2Builder>()
            .AddScoped<IBuildManifests<IIIF3.Manifest>, ManifestV3Builder>();

    /// <summary>
    /// Add required orchestrator dependencies
    /// </summary>
    public static IServiceCollection AddOrchestration(this IServiceCollection services,
        OrchestratorSettings settings)
    {
        var serviceCollection = services
            .AddSingleton<IAssetTracker, MemoryAssetTracker>()
            .AddSingleton<IImageOrchestrator>(sp =>
                ActivatorUtilities.CreateInstance<ImageOrchestrator>(sp,
                    sp.GetRequiredService<S3AmbientOriginStrategy>()))
            .AddSingleton<IFileSystem, FileSystem>()
            .AddSingleton<IOrchestrationQueue>(sp =>
                ActivatorUtilities.CreateInstance<BoundedChannelOrchestrationQueue>(sp,
                    settings.OrchestrateOnInfoJsonMaxCapacity));

        if (settings.OrchestrateOnInfoJson)
            serviceCollection.AddHostedService<OrchestrationQueueMonitor>();
        
        return serviceCollection;
    }

    /// <summary>
    /// Add HealthChecks for Database and downstream image-servers
    /// </summary>
    public static IServiceCollection ConfigureHealthChecks(this IServiceCollection services,
        IConfigurationSection proxySection,
        IConfiguration configuration)
    {
        var proxy = proxySection.Get<ProxySettings>();
        var tagsList = new[] { "detail-only" };
        var healthChecksBuilder = services
            .AddHealthChecks()
            .AddNpgSql(configuration.GetPostgresSqlConnection(), name: "Database")
            .AddProxyDestination(ProxyDestination.ImageServer, "Image Server")
            .AddProxyDestination(ProxyDestination.SpecialServer, "Special Server")
            .AddProxyDestination(ProxyDestination.Thumbs, "Thumbs", tags: tagsList);

        if (proxy.CanResizeThumbs)
        {
            healthChecksBuilder.AddProxyDestination(ProxyDestination.ResizeThumbs, "ThumbsResize", tags: tagsList);
        }

        return services;
    }

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
            .WithAmazonS3();

        return services;
    }
}
