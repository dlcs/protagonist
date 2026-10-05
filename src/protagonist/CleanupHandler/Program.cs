using CleanupHandler.Infrastructure;
using DLCS.AWS.SSM;
using DLCS.Core.Caching;
using DLCS.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace CleanupHandler;

public class Program
{
    public static async Task Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .CreateLogger();

        try
        {
            await CreateHostBuilder(args).Build().RunAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application start-up failed");
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                services
                    .Configure<CleanupHandlerSettings>(hostContext.Configuration)
                    .AddAws(hostContext.Configuration, hostContext.HostingEnvironment)
                    .AddDataAccess(hostContext.Configuration,
                        hostContext.Configuration.GetRequired<CleanupHandlerSettings>())
                    .AddCaching(hostContext.Configuration.GetSection("Caching").GetRequired<CacheSettings>())
                    .AddQueueMonitoring();
            })
            .UseSerilog((hostingContext, loggerConfiguration)
                => loggerConfiguration
                    .ReadFrom.Configuration(hostingContext.Configuration)
                    .Enrich.FromLogContext()
            )
            .ConfigureAppConfiguration((context, builder) =>
            {
                builder.AddSystemsManager(context);
            });
}
