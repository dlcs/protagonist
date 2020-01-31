using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Thumbs
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }
        
        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, builder) =>
                {
                    builder.AddSystemsManager(configurationSource =>
                    {
                        configurationSource.Path = "/thumbs/";
                        
                        // TODO - what's a sensible value here?
                        configurationSource.ReloadAfter = TimeSpan.FromMinutes(90);

                        // Using ParameterStore optional if Development
                        configurationSource.Optional = context.HostingEnvironment.IsDevelopment();
                    });
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
