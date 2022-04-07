﻿using System;
using System.Net.Http;
using API.Client;
using DLCS.Core.Encryption;
using DLCS.Model.Assets;
using DLCS.Model.Assets.CustomHeaders;
using DLCS.Model.Auth;
using DLCS.Model.Customers;
using DLCS.Model.PathElements;
using DLCS.Repository;
using DLCS.Repository.Assets;
using DLCS.Repository.Assets.CustomHeaders;
using DLCS.Repository.Auth;
using DLCS.Repository.Caching;
using DLCS.Repository.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Orchestrator.Infrastructure.Deliverator;
using Orchestrator.Infrastructure.ReverseProxy;
using Orchestrator.Settings;

namespace Orchestrator.Infrastructure
{
    public static class ServiceCollectionX
    {
        /// <summary>
        /// Add all dataaccess dependencies 
        /// </summary>
        public static IServiceCollection AddDataAccess(this IServiceCollection services, IConfiguration configuration)
            => services
                .AddSingleton<ICustomerRepository, DapperCustomerRepository>()
                .AddSingleton<IPathCustomerRepository, CustomerPathElementRepository>()
                .AddSingleton<IAssetRepository, DapperAssetRepository>()
                .AddSingleton<IThumbRepository, ThumbRepository>()
                .AddSingleton<IThumbnailPolicyRepository, ThumbnailPolicyRepository>()
                .AddSingleton<ICredentialsRepository, DapperCredentialsRepository>()
                .AddSingleton<IAuthServicesRepository, DapperAuthServicesRepository>()
                .AddSingleton<ICustomHeaderRepository, DapperCustomHeaderRepository>()
                .AddScoped<ICustomerOriginStrategyRepository, CustomerOriginStrategyRepository>()
                .AddDbContext<DlcsContext>(opts =>
                    opts.UseNpgsql(configuration.GetConnectionString("PostgreSQLConnection"))
                );

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
                .AddSingleton<DeliveratorApiAuth>()
                .AddSingleton<IEncryption, SHA256>()
                .AddHttpClient<IDlcsApiClient, DeliveratorApiClient>(client =>
                {
                    client.DefaultRequestHeaders.WithRequestedBy();
                    client.BaseAddress = apiRoot;
                });

            return services;
        }

        /// <summary>
        /// Add HealthChecks for Database and downstream image-servers
        /// </summary>
        public static IServiceCollection ConfigureHealthChecks(this IServiceCollection services,
            IConfigurationSection reverseProxySection,
            IConfiguration configuration)
        {
            var reverseProxySettings = reverseProxySection.Get<ReverseProxySettings>();
            var tagsList = new[] { "detail-only" };
            var healthChecksBuilder = services
                .AddHealthChecks()
                .AddNpgSql(configuration.GetPostgresSqlConnection(), name: "Database")
                .AddUrlGroup(reverseProxySettings.GetAddressForProxyTarget(ProxyDestination.ImageServer),
                    name: "Image Server")
                .AddUrlGroup(new Uri(reverseProxySettings.GetAddressForProxyTarget(ProxyDestination.Thumbs)!, "/ping"),
                    name: "Thumbs", tags: tagsList);
            
            var resizeThumbs = reverseProxySettings.GetAddressForProxyTarget(ProxyDestination.ResizeThumbs);
            if (resizeThumbs != null)
            {
                healthChecksBuilder.AddUrlGroup(new Uri(resizeThumbs, "/ping"), name: "ThumbsResize", tags: tagsList);
            }

            return services;
        }
    }
}