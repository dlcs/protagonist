using DLCS.HydraModel.Settings;
using DLCS.Mock.ApiApp;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;

namespace DLCS.Mock
{
    public class Startup
    {
        private const string Iso8601DateFormatString = "O";
        
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<HydraSettings>(Configuration.GetSection("Hydra"));
            var hydraSettings = Configuration.GetSection("Hydra").Get<HydraSettings>();
            services.AddSingleton<MockModel>(new MockModel(hydraSettings));
            services.AddControllers(options =>
            {
                options.Filters.Add(typeof(AddHydraApiHeaderFilter));
            })
            .AddNewtonsoftJson(options =>
            {
                var jsonSettings = options.SerializerSettings;
                jsonSettings.DateFormatString = Iso8601DateFormatString;
                jsonSettings.DateFormatHandling = DateFormatHandling.IsoDateFormat;
                options.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
                options.SerializerSettings.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter());
                jsonSettings.Formatting = Formatting.Indented;
            });
            services.AddSwaggerGen(c => { c.SwaggerDoc("v1", new OpenApiInfo {Title = "DLCS.Mock", Version = "v1"}); });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "DLCS.Mock v1"));
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
        }
    }
}