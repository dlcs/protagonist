using DLCS.Repository;
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
        services.AddAws(configuration, webHostEnvironment);
        
        services
            .AddHealthChecks()
            .AddNpgSql(configuration.GetPostgresSqlConnection());

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
            .UseHealthChecks("/ping")
            .UseEndpoints(endpoints => endpoints.MapControllers());
    }
}