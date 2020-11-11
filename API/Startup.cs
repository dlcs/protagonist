using System.Collections.Generic;
using Amazon.S3;
using API.Auth;
using API.Infrastructure;
using API.Settings;
using DLCS.Model.Storage;
using DLCS.Repository.Storage.S3;
using DLCS.Web.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
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
            
            services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy",
                    builder => builder
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .SetIsOriginAllowed(host => true)
                        .AllowCredentials());
            });

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

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILogger<Startup> logger)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            
            var applicationOptions = configuration.Get<ApiSettings>();
            var pathBase = applicationOptions.PathBase;
            var havePathBase = !string.IsNullOrEmpty(pathBase);
            if (havePathBase)
            {
                logger.LogInformation("Using PathBase '{pathBase}'", pathBase);
                app.UsePathBase($"/{pathBase}");
            }
            else
            {
                logger.LogInformation("No PathBase specified");
            }

            app
                .UseSwaggerWithUI(pathBase)
                .UseRouting()
                .UseSerilogRequestLogging()
                .UseCors("CorsPolicy")
                .UseAuthentication()
                .UseAuthorization()
                .UseHealthChecks("/ping")
                .UseEndpoints(endpoints => 
                    endpoints
                        .MapControllers()
                        .RequireAuthorization())
                ;
        }
    }
}