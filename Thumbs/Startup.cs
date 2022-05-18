using System.Collections.Generic;
using Amazon.S3;
using DLCS.AWS.Configuration;
using DLCS.AWS.S3;
using DLCS.Model.Assets;
using DLCS.Model.Customers;
using DLCS.Model.PathElements;
using DLCS.Repository;
using DLCS.Repository.Assets;
using DLCS.Repository.Caching;
using DLCS.Repository.Customers;
using DLCS.Repository.Settings;
using DLCS.Web.Middleware;
using DLCS.Web.Requests.AssetDelivery;
using DLCS.Web.Response;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Serilog;

namespace Thumbs
{
    public class Startup
    {
        public Startup(IConfiguration configuration, IWebHostEnvironment webHostEnvironment)
        {
            this.configuration = configuration;
            this.webHostEnvironment = webHostEnvironment;
        }

        private readonly IConfiguration configuration;
        private readonly IWebHostEnvironment webHostEnvironment;

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddHealthChecks()
                .AddNpgSql(configuration.GetPostgresSqlConnection());
            services.AddLazyCache();
            services.AddSingleton<AssetDeliveryPathParser>();
            services.AddSingleton<ICustomerRepository, DapperCustomerRepository>();
            services.AddSingleton<IPathCustomerRepository, CustomerPathElementRepository>();
            services.AddSingleton<IThumbRepository, ThumbRepository>();
            services.AddSingleton<IThumbReorganiser, ThumbReorganiser>();
            services.AddSingleton<IThumbnailPolicyRepository, ThumbnailPolicyRepository>();
            services.AddSingleton<IAssetRepository, DapperAssetRepository>();
            services.AddTransient<IAssetPathGenerator, ConfigDrivenAssetPathGenerator>();

            services
                .AddSingleton<IBucketReader, S3BucketReader>()
                .AddSingleton<IBucketWriter, S3BucketWriter>()
                .AddSingleton<IStorageKeyGenerator, S3StorageKeyGenerator>()
                .SetupAWS(configuration, webHostEnvironment)
                .WithAmazonS3();

            services
                .Configure<ThumbsSettings>(configuration.GetSection("Thumbs"))
                .Configure<PathTemplateOptions>(configuration.GetSection("PathRules"))
                .Configure<CacheSettings>(configuration.GetSection("Caching"));

            // Use x-forwarded-host and x-forwarded-proto to set httpContext.Request.Host and .Scheme respectively
            services.Configure<ForwardedHeadersOptions>(opts =>
            {
                opts.ForwardedHeaders = ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedProto;
            });
            services.AddHttpContextAccessor();

            services.PostConfigure<PathTemplateOptions>(opts =>
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

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory)
        {
            app.UseForwardedHeaders();
            loggerFactory.AddSerilog();
            
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            
            app.UseRouting();
            // TODO: Consider better caching solutions
            app.UseResponseCaching();
            var respondsTo = configuration.GetValue<string>("RespondsTo", "thumbs");
            var logger = loggerFactory.CreateLogger<Startup>();
            logger.LogInformation("ThumbsMiddleware mapped to '/{RespondsTo}/*'", respondsTo);
            app.UseEndpoints(endpoints =>
            {
                endpoints.Map($"/{respondsTo}/{{*any}}",
                    endpoints.CreateApplicationBuilder()
                        .UseMiddleware<AlwaysCorsMiddleware>()
                        .UseMiddleware<StatusCodeExceptionHandlerMiddleware>()
                        .UseMiddleware<ThumbsMiddleware>()
                        .Build());
                endpoints.MapHealthChecks("/ping");
            });
        }
    }
}
