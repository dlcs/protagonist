using Engine.Infrastructure;
using Serilog;

public class Startup
{
    private readonly IConfiguration configuration;
    private readonly IWebHostEnvironment webHostEnvironment;

    public Startup(IConfiguration configuration, IWebHostEnvironment webHostEnvironment)
    {
        this.configuration = configuration;
        this.webHostEnvironment = webHostEnvironment;
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services
            .AddAws(configuration, webHostEnvironment)
            .AddQueueMonitoring()
            .ConfigureHealthChecks(configuration);

        services.AddControllers();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILogger<Startup> logger)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        
        app.UseRouting()
            .UseSerilogRequestLogging()
            .UseCors()
            .UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapConfiguredHealthChecks();
            });
    }
}