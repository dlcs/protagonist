using Amazon.S3;
using API.Auth;
using API.Infrastructure;
using API.Settings;
using DLCS.Model.Storage;
using DLCS.Repository.Storage.S3;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Serilog;

namespace API
{
    public class Startup
    {
        private const string Iso8601DateFormatString = "O";
        private readonly IConfiguration configuration;
        
        public Startup(IConfiguration configuration)
        {
            this.configuration = configuration;
        }
        
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<ApiSettings>(configuration);
            var apiSettings = configuration.Get<ApiSettings>();
            
            services.AddHttpClient();
            services.AddHttpClient("dlcs-api", c =>
            {
                c.BaseAddress = apiSettings.DLCS.Root;
                c.DefaultRequestHeaders.Add("User-Agent", "DLCS-APIv2-Protagonist");
            });

            services
                .ConfigureMediatR()
                .ConfigureSwagger()
                .AddAWSService<IAmazonS3>()
                .AddSingleton<IBucketReader, BucketReader>();;

            services.AddDlcsDelegatedBasicAuth(options => options.Realm = "DLCS-API");

            services
                .AddControllers()
                .AddNewtonsoftJson(options =>
                {
                    var jsonSettings = options.SerializerSettings;
                    jsonSettings.DateFormatString = Iso8601DateFormatString;
                    jsonSettings.DateFormatHandling = DateFormatHandling.IsoDateFormat;
                    options.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
                    options.SerializerSettings.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter());
                    jsonSettings.Formatting = Formatting.Indented;
                })
                .SetCompatibilityVersion(CompatibilityVersion.Latest);

            services
                .AddHealthChecks()
                .AddUrlGroup(apiSettings.DLCS.Root, "DLCS API");
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseSwagger().UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v2/swagger.json", "DLCS API"));

            app.UseRouting()
                .UseSerilogRequestLogging()
                .UseCors()
                .UseAuthentication()
                .UseAuthorization()
                .UseHealthChecks("/ping")
                .UseEndpoints(endpoints => endpoints.MapControllers().RequireAuthorization());
        }
    }
}