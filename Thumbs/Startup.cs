using System.Collections.Generic;
using Amazon.S3;
using DLCS.Model.Assets;
using DLCS.Model.Customer;
using DLCS.Model.PathElements;
using DLCS.Model.Storage;
using DLCS.Repository;
using DLCS.Repository.Assets;
using DLCS.Repository.Settings;
using DLCS.Repository.Storage.S3;
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
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddHealthChecks()
                .AddNpgSql(Configuration.GetPostgresSqlConnection());
            services.AddLazyCache();
            services.AddDefaultAWSOptions(Configuration.GetAWSOptions());
            services.AddAWSService<IAmazonS3>();
            services.AddSingleton<IBucketReader, BucketReader>();
            services.AddSingleton<AssetDeliveryPathParser>();
            services.AddSingleton<ICustomerRepository, CustomerRepository>();
            services.AddSingleton<IPathCustomerRepository, CustomerPathElementRepository>();
            services.AddSingleton<IThumbRepository, ThumbRepository>();
            services.AddSingleton<IThumbReorganiser, ThumbReorganiser>();
            services.AddSingleton<IThumbnailPolicyRepository, ThumbnailPolicyRepository>();
            services.AddSingleton<IAssetRepository, AssetRepository>();
            services.AddTransient<IAssetPathGenerator, ConfigDrivenAssetPathGenerator>();

            services.Configure<ThumbsSettings>(Configuration.GetSection("Thumbs"));
            services.Configure<PathTemplateOptions>(Configuration.GetSection("PathRules"));

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
            var respondsTo = Configuration.GetValue<string>("RespondsTo", "thumbs");
            var logger = loggerFactory.CreateLogger<Startup>();
            logger.LogInformation("ThumbsMiddleware mapped to '/{RespondsTo}/*'", respondsTo);
            app.UseEndpoints(endpoints =>
            {
                // 'normal' thumbs handling - will only return if we have it
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
