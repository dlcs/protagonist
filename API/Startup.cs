using API.Auth;
using API.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace API
{
    public class Startup
    {
        private readonly IConfiguration configuration;
        
        public Startup(IConfiguration configuration)
        {
            this.configuration = configuration;
        }
        
        public void ConfigureServices(IServiceCollection services)
        {
            var apiSettings = configuration.Get<ApiSettings>();
            
            services.AddHttpClient();
            services.AddHttpClient("dlcs-api", c =>
            {
                c.BaseAddress = apiSettings.DLCS.Root;
                c.DefaultRequestHeaders.Add("User-Agent", "DLCS-APIv2-Protagonist");
            });

            services.AddDlcsDelegatedBasicAuth(options => options.Realm = "DLCS-API");

            services
                .AddControllers()
                .SetCompatibilityVersion(CompatibilityVersion.Latest)
                .AddJsonOptions(
                    options =>
                    {
                        options.JsonSerializerOptions.IgnoreNullValues = true;
                        options.JsonSerializerOptions.WriteIndented = true;
                    });

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