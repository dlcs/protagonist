using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Portal.Tests.Integration.Infrastructure
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TStartup"></typeparam>
    public class ProtagonistAppFactory<TStartup> : WebApplicationFactory<TStartup>
        where TStartup: class
    {
        private readonly Dictionary<string, string> configuration = new();

        /// <summary>
        /// Specify connection string to use for dlcsContext when building services
        /// </summary>
        /// <param name="connectionString"></param>
        /// <returns></returns>
        public ProtagonistAppFactory<TStartup> WithConnectionString(string connectionString)
        {
            configuration["ConnectionStrings:PostgreSQLConnection"] = connectionString;
            return this;
        }
        
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            var projectDir = Directory.GetCurrentDirectory();
            var configPath = Path.Combine(projectDir, "appsettings.Testing.json");

            builder
                .ConfigureAppConfiguration((context, conf) =>
                {
                    conf.AddJsonFile(configPath);
                    conf.AddInMemoryCollection(configuration);
                })
                .ConfigureTestServices(services =>
                {
                    services.AddAuthentication("Test")
                        .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                            "Test", _ => {});
                });
                //.UseEnvironment("Testing");  // TODO This causes weirdness with AWS
        }
    }
}