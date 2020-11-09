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

            services
                .AddControllers()
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

            app.UseRouting()
                .UseSerilogRequestLogging()
                .UseCors()
                .UseHealthChecks("/ping")
                .UseEndpoints(endpoints => endpoints.MapControllers());
        }
    }
}