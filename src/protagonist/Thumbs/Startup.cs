using DLCS.Model.Assets;
using DLCS.Model.Customers;
using DLCS.Model.PathElements;
using DLCS.Repository;
using DLCS.Repository.Assets;
using DLCS.Repository.Caching;
using DLCS.Repository.Customers;
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
using Serilog;
using Thumbs.Infrastructure;
using Thumbs.Settings;

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
            services
                .Configure<ThumbsSettings>(configuration.GetSection("Thumbs"))
                .Configure<PathTemplateOptions>(configuration.GetSection("PathRules"))
                .Configure<CacheSettings>(configuration.GetSection("Caching"));
            
            var thumbSettings = configuration.GetSection("Thumbs").Get<ThumbsSettings>();
            
            services
                .AddHealthChecks()
                .AddNpgSql(configuration.GetPostgresSqlConnection());

            services
                .AddLazyCache()
                .AddThumbnailHandling(thumbSettings)
                .AddAws(configuration, webHostEnvironment)
                .AddSingleton<AssetDeliveryPathParser>()
                .AddSingleton<ICustomerRepository, DapperCustomerRepository>()
                .AddSingleton<IPathCustomerRepository, CustomerPathElementRepository>()
                .AddSingleton<IAssetRepository, DapperAssetRepository>()
                .AddTransient<IAssetPathGenerator, ConfigDrivenAssetPathGenerator>();

            // Use x-forwarded-host and x-forwarded-proto to set httpContext.Request.Host and .Scheme respectively
            services.Configure<ForwardedHeadersOptions>(opts =>
            {
                opts.ForwardedHeaders = ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedProto;
            });
            services.AddHttpContextAccessor();
            services.HandlePathTemplates();
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
