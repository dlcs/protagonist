using Amazon.S3;
using DLCS.Model.Assets;
using DLCS.Model.Customer;
using DLCS.Model.PathElements;
using DLCS.Model.Storage;
using DLCS.Repository;
using DLCS.Repository.Assets;
using DLCS.Repository.Settings;
using DLCS.Repository.Storage.S3;
using DLCS.Web.Requests.AssetDelivery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
            services.AddCors();
            services.AddLazyCache();
            services.AddDefaultAWSOptions(Configuration.GetAWSOptions());
            services.AddAWSService<IAmazonS3>();
            services.AddSingleton<IBucketReader, BucketReader>();
            services.AddSingleton<AssetDeliveryPathParser>();
            services.AddSingleton<ICustomerRepository, CustomerRepository>();
            services.AddSingleton<IPathCustomerRepository, CustomerPathElementRepository>();
            services.AddSingleton<IThumbRepository, ThumbRepository>();
            services.AddSingleton<IAssetRepository, AssetRepository>();

            services.Configure<ThumbsSettings>(Configuration.GetSection("Repository"));
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            
            app.UseRouting();
            app.UseCors(); 
            // TODO: Consider better caching solutions
            app.UseResponseCaching();
            app.UseEndpoints(endpoints =>
            {
                endpoints.Map("/thumbs/{*any}", 
                    endpoints.CreateApplicationBuilder()
                    .UseMiddleware<ThumbsMiddleware>()
                    .Build());
                endpoints.MapHealthChecks("/ping");
            });
            
        }
    }
}
