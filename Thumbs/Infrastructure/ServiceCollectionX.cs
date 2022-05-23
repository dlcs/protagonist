﻿using System.Collections.Generic;
using DLCS.AWS.Configuration;
using DLCS.AWS.S3;
using DLCS.Model.Assets;
using DLCS.Repository.Assets;
using DLCS.Web.Response;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Serilog;
using Thumbs.Reorganising;
using Thumbs.Settings;

namespace Thumbs.Infrastructure
{
    public static class ServiceCollectionX
    {
        /// <summary>
        /// Add required dependencies for handling thumbnails
        /// </summary>
        public static IServiceCollection AddThumbnailHandling(this IServiceCollection services, ThumbsSettings settings)
        {
            services
                .AddSingleton<ThumbnailHandler>()
                .AddSingleton<IThumbnailPolicyRepository, ThumbnailPolicyRepository>();

            if (settings.EnsureNewThumbnailLayout)
            {
                // If reorganising thumb, register IThumbReorganiser and ReorganisingThumbRepository which is a
                // decorator for default IThumbRespositry
                Log.Information("Thumbs supports reorganising thumbs");
                services
                    .AddSingleton<ThumbRepository>()
                    .AddSingleton<IThumbRepository>(provider =>
                        ActivatorUtilities.CreateInstance<ReorganisingThumbRepository>(
                            provider,
                            provider.GetService<ThumbRepository>()!))
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

        /// <summary>
        /// Parse OverridesAsJson appSetting to strongly typed dictionary
        /// </summary>
        public static IServiceCollection HandlePathTemplates(this IServiceCollection services)
            => services.PostConfigure<PathTemplateOptions>(opts =>
            {
                if (!string.IsNullOrEmpty(opts.OverridesAsJson))
                {
                    var overridesDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(opts.OverridesAsJson);
                    foreach (var (key, value) in overridesDict)
                    {
                        opts.Overrides.Add(key, value);
                    }
                }
            });
    }
}