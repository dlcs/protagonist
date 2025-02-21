using DLCS.AWS.SSM;
using DLCS.Repository;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Configuring IHost");
    var host = Host.CreateDefaultBuilder(args)
        .ConfigureServices(collection => { collection.AddSingleton<Migrator>(); })
        .ConfigureAppConfiguration((context, builder) => { builder.AddSystemsManager(context); })
        .UseSerilog()
        .Build();

    Log.Information("Executing Migrator");
    var migrator = host.Services.GetRequiredService<Migrator>();
    migrator.Execute();
    Log.Information("Migrator Ran");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Migrator failed");
}
finally
{
    Log.CloseAndFlush();
}

class Migrator
{
    private readonly ILogger<Migrator> logger;
    private readonly IConfiguration configuration;

    public Migrator(ILogger<Migrator> logger, IConfiguration configuration)
    {
        this.logger = logger;
        this.configuration = configuration;
    }

    public void Execute()
    {
        var connStr = configuration.GetConnectionString("PostgreSQLConnection");
        foreach (var part in connStr.Split(";"))
        {
            var lowered = part.ToLower();
            if (lowered.StartsWith("server") || lowered.StartsWith("database"))
            {
                logger.LogInformation("Got connstr part {StringPart}", lowered);
            }
        }
        
        DlcsContextConfiguration.TryRunMigrations(configuration, logger);
    }
}
