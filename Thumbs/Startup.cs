using Amazon.S3;
using DLCS.Model.Assets;
using DLCS.Model.Customer;
using DLCS.Model.PathElements;
using DLCS.Model.Storage;
using DLCS.Repository;
using DLCS.Repository.Assets;
using DLCS.Repository.Storage.S3;
using DLCS.Web.Requests.AssetDelivery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
            services.AddHealthChecks();
            services.AddCors();
            services.AddDefaultAWSOptions(Configuration.GetAWSOptions());
            services.AddAWSService<IAmazonS3>();
            services.AddSingleton<IBucketReader, BucketReader>();
            services.AddSingleton<AssetDeliveryPathParser>();
            services.AddSingleton<IMemoryCache, MemoryCache>();
            services.AddSingleton<ICustomerRepository, CustomerRepository>();
            services.AddSingleton<IPathCustomerRepository, CustomerPathElementRepository>();
            services.AddSingleton<IThumbRepository, ThumbRepository>();
            services.AddSingleton<IAssetRepository, AssetRepository>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseRouting();
            app.UseCors(); 
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
