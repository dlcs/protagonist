using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;

public class Program
{
    public static void Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .CreateLogger();

        try
        {
            CreateHostBuilder(args).Build().Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application start-up failed");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                services.Configure<HostOptions>(options =>
                {
                    options.ShutdownTimeout = TimeSpan.FromSeconds(10);
                });
            })
            .UseSerilog((hostingContext, loggerConfiguration)
                => loggerConfiguration
                    .ReadFrom.Configuration(hostingContext.Configuration)
            )
            .ConfigureAppConfiguration((context, builder) =>
            {
                if (context.HostingEnvironment.IsProduction())
                {
                    builder.AddSystemsManager(configurationSource =>
                    {
                        configurationSource.Path = "/protagonist/";
                        configurationSource.ReloadAfter = TimeSpan.FromMinutes(90);
                    });
                }
            })
            .ConfigureWebHostDefaults(webBuilder => { webBuilder.UseStartup<Startup>(); });
}