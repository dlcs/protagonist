using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Thumbs
{
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
                .UseSerilog((hostingContext, loggerConfiguration)
                    => loggerConfiguration.ReadFrom.Configuration(hostingContext.Configuration)
                )
                .ConfigureAppConfiguration((context, builder) =>
                {
                    var isDevelopment = context.HostingEnvironment.IsDevelopment();
                    builder.AddSystemsManager(configurationSource =>
                    {
                        configurationSource.Path = "/thumbs/";
                        configurationSource.ReloadAfter = TimeSpan.FromMinutes(90);
                    
                        // Using ParameterStore optional if Development
                        configurationSource.Optional = isDevelopment;
                    });

                    // If development then ensure appsettings.Development.json wins
                    if (isDevelopment)
                    {
                        builder.AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true);
                    }
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
